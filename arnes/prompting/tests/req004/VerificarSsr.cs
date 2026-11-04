// Host auxiliar: componentes, servicios y MySQL reales, cookies de prueba propias.
// No modifica Program.cs ni la autenticación OIDC de SolarUY. No acredita aceptación externa.
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Components;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;

internal static class VerificarSsr
{
    private static int _comprobaciones;

    public static async Task EjecutarAsync(bool preview)
    {
        var raiz = Directory.GetCurrentDirectory();
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(raiz, "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var opciones = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var destino = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync("artifacts/req004/destino-aislado.json"))
            .GetProperty("Base").GetString()!;
        if (!destino.StartsWith("solaruy_req004_", StringComparison.Ordinal)) throw new InvalidOperationException();
        opciones.Database = destino;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(App).Assembly.FullName,
            ContentRootPath = Path.Combine(raiz, "SolarUY"), EnvironmentName = "Production"
        });
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:8184");
        builder.WebHost.UseSetting(WebHostDefaults.StaticWebAssetsKey,
            Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.runtime.json"));
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        var fallo = new FalloSsrInterceptor();
        builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddDbContext<SolarUyDbContext>(config =>
        {
            PersistenciaConfiguration.ConfigurarContexto(config, opciones.ConnectionString);
            config.AddInterceptors(fallo);
        });
        builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
        builder.Services.AddScoped<INodoRepository, NodoRepository>();
        builder.Services.AddScoped<SolarUY.Application.Monitoreo.IMedicionRepository, MedicionRepositorySinLecturas>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(config =>
        {
            config.Cookie.Name = "SolarUY.Req004.Auxiliar";
            config.LoginPath = "/cuenta/iniciar-sesion";
            config.AccessDeniedPath = "/cuenta/acceso-denegado";
        });
        builder.Services.AddDataProtection().SetApplicationName("SolarUY.Req004.Auxiliar");
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        app.UseStaticFiles();
        app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.endpoints.json"));
        app.MapGet("/_auxiliar/sesion/{rol}", async (HttpContext contexto, string rol) =>
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "Identidad auxiliar req004"), new Claim(ClaimTypes.Role, rol) };
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Redirect("/parques-solares");
        });
        app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);
        await app.StartAsync();
        try
        {
            using var scope = app.Services.CreateScope();
            var contexto = scope.ServiceProvider.GetRequiredService<SolarUyDbContext>();
            var parque = ParqueSolar.Crear("SSR req004 " + Guid.NewGuid().ToString("N")[..8], "Prueba aislada");
            var otro = ParqueSolar.Crear("SSR otro " + Guid.NewGuid().ToString("N")[..8], "Prueba aislada");
            var repositorio = new ParqueSolarRepository(contexto);
            await repositorio.GuardarAsync(parque); await repositorio.GuardarAsync(otro);
            contexto.ChangeTracker.Clear();
            var ruta = $"/parques-solares/{parque.ID:D}/nodos/nuevo";
            var otraRuta = $"/parques-solares/{otro.ID:D}/nodos/nuevo";
            using var anonimo = Cliente(); using var admin = Cliente(); using var sinRol = Cliente();
            await admin.GetAsync("/_auxiliar/sesion/Administrador");
            await sinRol.GetAsync("/_auxiliar/sesion/SinRol");
            foreach (var cliente in new[] { anonimo, sinRol })
            {
                var carga = await cliente.GetAsync(ruta);
                Exigir(carga.StatusCode == HttpStatusCode.Redirect, "carga-protegida");
                var formularioAjeno = await admin.GetStringAsync(ruta);
                var post = await EnviarAsync(cliente, ruta, formularioAjeno);
                Exigir(post.StatusCode == HttpStatusCode.Redirect, "post-protegido");
            }
            var html = await admin.GetStringAsync(ruta);
            Exigir(html.Contains("<legend>Nodo</legend>") && html.Contains("<legend>Inversor</legend>")
                && html.Contains("<legend>Sensor</legend>"), "tres-secciones-reales");
            Exigir(html.Contains("Parque de destino:") && html.Contains(parque.ID.ToString("D")), "parque-identificado");
            Exigir(WebUtility.HtmlDecode(await admin.GetStringAsync($"/parques-solares/{parque.ID:D}")).Contains("+ Agregar nodo"), "boton-administrador");
            Exigir(!WebUtility.HtmlDecode(await sinRol.GetStringAsync($"/parques-solares/{parque.ID:D}")).Contains("+ Agregar nodo"), "boton-oculto-sin-rol");
            var sinToken = await EnviarAsync(admin, ruta, html, incluirToken: false);
            Exigir(sinToken.StatusCode == HttpStatusCode.BadRequest, "antifalsificacion-obligatoria");

            foreach (var campo in new[] { "Nombre", "Latitud", "Longitud", "NombreInversor", "NumeroSerieInversor", "NombreSensor", "NumeroSerieSensor" })
            {
                html = await admin.GetStringAsync(ruta);
                var respuesta = await EnviarAsync(admin, ruta, html, new() { ["Modelo." + campo] = "" });
                var rechazo = await respuesta.Content.ReadAsStringAsync();
                Exigir(respuesta.IsSuccessStatusCode && rechazo.Contains("validation-message") && !rechazo.Contains("Nodo registrado"), "post-campo-obligatorio-" + campo);
                Exigir(rechazo.Contains(campo == "NombreSensor" ? "value=\"Inversor req004\"" : "value=\"Sensor req004\""), "conservacion-entrada-" + campo);
            }
            foreach (var campo in new[] { "Latitud", "Longitud" })
            {
                html = await admin.GetStringAsync(ruta);
                var respuesta = await EnviarAsync(admin, ruta, html, new() { ["Modelo." + campo] = "no-numero" });
                var rechazo = await respuesta.Content.ReadAsStringAsync();
                Exigir(respuesta.IsSuccessStatusCode && rechazo.Contains("validation-message") && !rechazo.Contains("Nodo registrado"), "conversion-numerica-" + campo);
            }
            Exigir(await contexto.Nodos.CountAsync(n => n.Parque.ID == parque.ID) == 0, "posts-invalidos-sin-escrituras");
            html = await admin.GetStringAsync(ruta);
            var valida = await EnviarAsync(admin, ruta, html);
            var confirmacion = await valida.Content.ReadAsStringAsync();
            Exigir(valida.IsSuccessStatusCode && confirmacion.Contains("Nodo registrado") && confirmacion.Contains("Cantidad de paneles"), "post-ssr-confirmado");
            var recargado = (await repositorio.ObtenerPorIdAsync(parque.ID))!;
            Exigir(recargado.Nodos.Count == 1 && recargado.Nodos.Single().Inversor.Sensor.Endpoint is null, "post-persistido");
            html = await admin.GetStringAsync(otraRuta);
            var cambioRuta = await EnviarAsync(admin, otraRuta, html,
                new() { ["Modelo.ParqueId"] = parque.ID.ToString("D"), ["ParqueId"] = parque.ID.ToString("D"), ["Modelo.EndpointSensor"] = "sin protocolo" });
            Exigir((await cambioRuta.Content.ReadAsStringAsync()).Contains("Nodo registrado")
                && (await repositorio.ObtenerPorIdAsync(otro.ID))!.Nodos.Count == 1
                && (await repositorio.ObtenerPorIdAsync(parque.ID))!.Nodos.Count == 1, "destino-solo-desde-ruta");

            foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
            {
                var inexistente = $"/parques-solares/{id:D}/nodos/nuevo";
                var pagina = await admin.GetStringAsync(inexistente);
                Exigir(pagina.Contains("No se encontr") && !pagina.Contains("<legend>Nodo"), "get-inexistente");
                var post = await EnviarAsync(admin, inexistente, await admin.GetStringAsync(ruta));
                // No se registra un manejador de alta si el parque no existe: SSR rechaza el POST.
                Exigir(post.StatusCode == HttpStatusCode.BadRequest ||
                    (await post.Content.ReadAsStringAsync()).Contains("No se encontr"), "post-inexistente-rechazado");
            }
            Exigir((await admin.GetAsync("/parques-solares/no-guid/nodos/nuevo")).StatusCode == HttpStatusCode.NotFound, "guid-malformado");
            fallo.Activo = true;
            var error = await EnviarAsync(admin, ruta, await admin.GetStringAsync(ruta));
            var errorHtml = await error.Content.ReadAsStringAsync();
            Exigir(errorHtml.Contains("No se pudo registrar el nodo. Intent") && !errorHtml.Contains("Nodo registrado"), "mensaje-tecnico-generico");
            Exigir((await repositorio.ObtenerPorIdAsync(parque.ID))!.Nodos.Count == 1, "fallo-ssr-sin-parciales");
            fallo.Activo = false;
            var recuperacion = await EnviarAsync(admin, ruta, await admin.GetStringAsync(ruta));
            Exigir((await recuperacion.Content.ReadAsStringAsync()).Contains("Nodo registrado"), "recuperacion-ssr");
            await File.WriteAllTextAsync("artifacts/req004/ssr-resultados.json", JsonSerializer.Serialize(new
            {
                correcto = true, comprobaciones = _comprobaciones, ruta,
                alcance = "HTTP SSR real, cookies auxiliares; no aceptación con identidad OIDC"
            }));
            Console.WriteLine("SSR comprobaciones: " + _comprobaciones);
            if (preview)
            {
                Console.WriteLine("Vista auxiliar en http://127.0.0.1:8184; datos y sesión de prueba.");
                await app.WaitForShutdownAsync();
            }
        }
        finally { await app.StopAsync(); }
    }

    private static HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false })
    { BaseAddress = new Uri("http://127.0.0.1:8184") };

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string ruta, string html,
        Dictionary<string, string>? cambios = null, bool incluirToken = true)
    {
        var datos = new Dictionary<string, string>
        {
            ["_handler"] = "alta-nodo", ["Modelo.Nombre"] = "Nodo req004", ["Modelo.Latitud"] = "0", ["Modelo.Longitud"] = "0",
            ["Modelo.NombreInversor"] = "Inversor req004", ["Modelo.NumeroSerieInversor"] = "I-SSR",
            ["Modelo.NombreSensor"] = "Sensor req004", ["Modelo.NumeroSerieSensor"] = "S-SSR", ["Modelo.EndpointSensor"] = ""
        };
        if (incluirToken)
            datos["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        if (cambios is not null) foreach (var cambio in cambios) datos[cambio.Key] = cambio.Value;
        return await cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
    }

    private static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new InvalidOperationException(criterio);
        _comprobaciones++;
        Console.WriteLine("OK " + criterio);
    }

    private sealed class FalloSsrInterceptor : SaveChangesInterceptor
    {
        public bool Activo { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => Activo ? throw new InvalidOperationException() : ValueTask.FromResult(result);
    }
}
