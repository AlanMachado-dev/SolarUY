// HTTP SSR real con componentes y MySQL reales. Cookies auxiliares locales; no aceptación OIDC externa.
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Components;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using static Integracion;

internal static class VerificarWeb
{
    private const string Direccion = "http://127.0.0.1:8192";
    private static readonly string Sufijo = Guid.NewGuid().ToString("N")[..8];

    public static async Task EjecutarAsync(bool preview)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(App).Assembly.FullName,
            ContentRootPath = Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"), EnvironmentName = "Production"
        });
        builder.WebHost.UseUrls(Direccion);
        builder.WebHost.UseSetting(WebHostDefaults.StaticWebAssetsKey, Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.runtime.json"));
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        var barrera = new BarreraGuardado();
        var conexion = VerificarMod002.Opciones(); conexion.Database = BasePrincipal();
        Exigir(conexion.Database.StartsWith("solaruy_mod002_"), "destino-web-aislado");
        builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddDbContext<SolarUyDbContext>(opciones =>
        {
            PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
            opciones.AddInterceptors(barrera);
        });
        builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
        builder.Services.AddScoped<INodoRepository, NodoRepository>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opciones =>
        {
            opciones.Cookie.Name = "SolarUY.Mod002.Auxiliar";
            opciones.LoginPath = "/cuenta/iniciar-sesion";
            opciones.AccessDeniedPath = "/cuenta/acceso-denegado";
        });
        builder.Services.AddDataProtection().SetApplicationName("SolarUY.Mod002.Auxiliar");
        builder.Services.AddAuthorization(); builder.Services.AddCascadingAuthenticationState();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery(); app.UseStaticFiles();
        app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.endpoints.json"));
        app.MapGet("/_auxiliar/sesion/{rol}", async (HttpContext contexto, string rol) =>
        {
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Identidad auxiliar mod002"),
                    new Claim(ClaimTypes.Role, rol) }, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Redirect("/parques-solares");
        });
        app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);
        await app.StartAsync();
        try
        {
            using var admin = Cliente(); using var operador = Cliente(); using var anonimo = Cliente();
            await admin.GetAsync("/_auxiliar/sesion/Administrador"); await operador.GetAsync("/_auxiliar/sesion/Operador");
            await using var contexto = Contexto();
            var parque = await CrearParque(admin, "Web A " + Sufijo);
            var otro = await CrearParque(admin, "Web B " + Sufijo);
            var detalle = $"/parques-solares/{parque:D}";
            var ruta = detalle + "/nodos/nuevo";
            var rutaOtro = $"/parques-solares/{otro:D}/nodos/nuevo";
            var html = await Pagina(admin, detalle + "?creado=true");
            Exigir(html.Contains("El parque solar se creó correctamente.") && html.Contains("role=\"status\""), "F01-alerta-en-detalle-creado");
            Exigir(html.Contains(ruta) && html.Contains("+ Agregar nodo") && (await Pagina(admin, ruta)).Contains(parque.ToString("D")), "F02-enlace-alta-mismo-parque");
            var invalido = await Enviar(admin, "/parques-solares/nuevo", new() { ["_handler"] = "alta-parque-solar", ["Modelo.Nombre"] = "", ["Modelo.Ubicacion"] = "Conservar ubicación" });
            Exigir(invalido.Estado == 200 && invalido.Html.Contains("validation-message") && invalido.Html.Contains("Conservar ubicación") && invalido.Ubicacion is null, "F03-alta-invalida-conserva-formulario");
            var cantidad = await contexto.ParquesSolares.CountAsync();
            await Pagina(admin, detalle + "?creado=true");
            Exigir(await contexto.ParquesSolares.CountAsync() == cantidad, "F04-recargar-sin-crear");
            Exigir(!(await Pagina(admin, detalle)).Contains("El parque solar se creó correctamente.")
                && !(await Pagina(admin, $"/parques-solares/{Guid.NewGuid():D}?creado=true")).Contains("El parque solar se creó correctamente."), "F05-alerta-solo-en-detalle-exitoso-con-indicador");
            Exigir((await Pagina(admin, detalle)).Contains("El parque solar todavía no tiene nodos."), "F06-coleccion-vacia");
            var datos = Datos("i-" + Sufijo, "s-" + Sufijo);
            var alta = await Enviar(admin, ruta, datos);
            Exigir(alta.Estado == 200 && alta.Html.Contains("Nodo registrado") && alta.Html.Contains(detalle), "F07-alta-y-retorno-existente");
            var nodo = await contexto.Nodos.Include(n => n.Inversor).ThenInclude(i => i.Sensor).SingleAsync(n => n.Parque.ID == parque);
            html = await Pagina(admin, detalle);
            foreach (var valor in new[] { nodo.ID.ToString("D"), nodo.Nombre, nodo.Latitud.ToString(System.Globalization.CultureInfo.CurrentCulture),
                nodo.Longitud.ToString(System.Globalization.CultureInfo.CurrentCulture), nodo.Inversor.ID.ToString("D"), nodo.Inversor.Nombre,
                nodo.Inversor.NumeroSerie, nodo.Inversor.Sensor.ID.ToString("D"), nodo.Inversor.Sensor.Nombre, nodo.Inversor.Sensor.NumeroSerie })
                Exigir(html.Contains(valor), "F07-dato-persistido-en-detalle");
            Exigir(html.Contains("Cantidad de paneles") && html.Contains("Sin endpoint"), "F09-paneles-y-sensor-sin-endpoint");
            Exigir((await Pagina(admin, detalle)).Contains(nodo.ID.ToString("D"))
                && !(await Pagina(admin, $"/parques-solares/{otro:D}")).Contains(nodo.ID.ToString("D")), "F08-recarga-y-aislamiento-parques");
            foreach (var destino in new[] { ruta, rutaOtro })
            foreach (var inversor in new[] { true, false })
            {
                var antes = await contexto.Nodos.CountAsync();
                var respuesta = await Enviar(admin, destino, Datos(inversor ? nodo.Inversor.NumeroSerie : Guid.NewGuid().ToString("N"),
                    inversor ? Guid.NewGuid().ToString("N") : nodo.Inversor.Sensor.NumeroSerie));
                ErrorCampo(respuesta.Html, inversor);
                Exigir(!respuesta.Html.Contains("Nodo registrado") && await contexto.Nodos.CountAsync() == antes,
                    "F10-F13-rechazo-global-sin-escritura-" + (destino == ruta) + "-" + inversor);
            }
            var ambos = await Enviar(admin, ruta, datos);
            ErrorCampo(ambos.Html, true); ErrorCampo(ambos.Html, false);
            Exigir(ambos.Html.Contains("value=\"Inversor web\"") && ambos.Html.Contains("value=\"Sensor web\""), "F14-conserva-formulario-ambos-errores");
            datos = Datos("corregido-i-" + Sufijo, "corregido-s-" + Sufijo);
            datos["Modelo.EndpointSensor"] = "endpoint auxiliar";
            Exigir((await Enviar(admin, ruta, datos)).Html.Contains("Nodo registrado")
                && (await Pagina(admin, detalle)).Contains("endpoint auxiliar"), "F14-F09-corregir-reenviar-endpoint");
            var espacios = await Enviar(admin, ruta, Datos(" \t" + nodo.Inversor.NumeroSerie + "  ", "  " + nodo.Inversor.Sensor.NumeroSerie + "\n"));
            ErrorCampo(espacios.Html, true); ErrorCampo(espacios.Html, false);
            Exigir(!espacios.Html.Contains("Nodo registrado"), "F15-trim-no-elude-unicidad");
            foreach (var serie in new[] { "á", "Á", "a", "A", "a a", "a  a" })
                Exigir((await Enviar(admin, ruta, Datos(serie + Sufijo, serie + Sufijo))).Html.Contains("Nodo registrado"), "F16-F17-distincion-y-tipos-independientes-" + serie);
            Exigir((await Enviar(admin, ruta, Datos(" " + new string('I', 247) + Sufijo + " ", " " + new string('S', 247) + Sufijo + " "))).Html.Contains("Nodo registrado"), "F18-limite255-recortado-formulario");
            foreach (var inversor in new[] { true, false })
            {
                var largo = await Enviar(admin, ruta, Datos(inversor ? new string('X', 256) : "libre-i", inversor ? "libre-s" : new string('X', 256)));
                Exigir(Regex.Match(largo.Html, "<div id=\"serie-" + (inversor ? "inversor" : "sensor") + "-error\">(.*?)</div>", RegexOptions.Singleline).Value.Contains("255"), "F18-limite256-campo-" + inversor);
            }
            foreach (var inversor in new[] { true, false })
                await Carrera(admin, contexto, barrera, ruta, inversor);

            var rutaDetalle = $"/parques-solares/{parque:D}";
            Exigir((await anonimo.GetAsync(rutaDetalle)).StatusCode == HttpStatusCode.Redirect, "F22-anonimo-detalle-protegido");
            Exigir(!(await Pagina(operador, rutaDetalle)).Contains("+ Agregar nodo"), "F22-operador-lee-sin-alta");
            foreach (var restringida in new[] { ruta, "/parques-solares/nuevo" })
            {
                Exigir((await operador.GetAsync(restringida)).StatusCode == HttpStatusCode.Redirect, "F22-get-alta-operador-denegado");
                // Antifalsificación emitida para la misma identidad, en la página de salida.
                var formulario = await Pagina(operador, "/parques-solares");
                Exigir((await Enviar(operador, restringida, restringida == ruta ? Datos("prohibido", "prohibido") :
                    new() { ["_handler"] = "alta-parque-solar", ["Modelo.Nombre"] = "prohibido", ["Modelo.Ubicacion"] = "aislado" }, formulario)).Estado == 302, "F22-post-alta-operador-denegado");
            }
            Exigir((await Pagina(admin, "/parques-solares")).Contains("Web A " + Sufijo), "F23-listado-conservado");
            var repetido = await Enviar(admin, "/parques-solares/nuevo", new() { ["_handler"] = "alta-parque-solar", ["Modelo.Nombre"] = "Web A " + Sufijo, ["Modelo.Ubicacion"] = "aislado" });
            Exigir(repetido.Html.Contains("Ya existe un parque solar con ese nombre."), "F23-nombre-unico-conservado");
            Exigir((await Pagina(admin, $"/parques-solares/{Guid.NewGuid():D}")).Contains("No se encontró el parque solar"), "F23-inexistente");
            await Sql(contexto, "RENAME TABLE Inversores TO mod002_inversores_ocultos");
            try { Exigir((await Pagina(admin, detalle)).Contains("No se pudo cargar la información."), "F23-error-carga-generico-real"); }
            finally { await Sql(contexto, "RENAME TABLE mod002_inversores_ocultos TO Inversores"); }
            var esperado = await contexto.Nodos.AsNoTracking().Where(n => n.Parque.ID == parque).OrderBy(n => n.Nombre).ThenBy(n => n.ID).Select(n => n.ID).ToArrayAsync();
            // El orden del servicio se comprueba contra los IDs renderizados, sin ordenar en Blazor.
            var htmlFinal = await Pagina(admin, detalle);
            Exigir(esperado.Select(id => htmlFinal.IndexOf($"id=\"nodo-{id:D}\"", StringComparison.Ordinal)).SequenceEqual(
                esperado.Select(id => htmlFinal.IndexOf($"id=\"nodo-{id:D}\"", StringComparison.Ordinal)).Order()), "orden-nodos-en-html");
            Exigir(await contexto.Nodos.CountAsync() == await contexto.Inversores.CountAsync()
                && await contexto.Nodos.CountAsync() == await contexto.Sensores.CountAsync(), "final-sin-registros-huerfanos");
            await File.WriteAllTextAsync("artifacts/mod002/web-resultados.json", JsonSerializer.Serialize(new
            { comprobaciones = Comprobaciones, parque, otro, alcance = "HTTP SSR real con cookies auxiliares, MySQL real; sin aceptación externa OIDC", direccion = Direccion + detalle }));
            Console.WriteLine("VERIFICACIONES WEB CORRECTAS; vista: " + Direccion + detalle);
            if (preview) await app.WaitForShutdownAsync();
        }
        finally { await app.StopAsync(); }
    }

    private static async Task Carrera(HttpClient admin, SolarUyDbContext contexto, BarreraGuardado barrera, string ruta, bool inversor)
    {
        var antes = await contexto.Nodos.CountAsync();
        var prefijo = Guid.NewGuid().ToString("N");
        barrera.Iniciar();
        var respuestas = await Task.WhenAll(
            Enviar(admin, ruta, Datos(prefijo + "i", prefijo + "s")),
            Enviar(admin, ruta, Datos(prefijo + "i" + (inversor ? "" : "2"), prefijo + "s" + (inversor ? "2" : ""))));
        barrera.Activa = false;
        Exigir(respuestas.Count(r => r.Html.Contains("Nodo registrado")) == 1, "F19-F20-una-confirmacion-" + inversor);
        ErrorCampo(respuestas.Single(r => !r.Html.Contains("Nodo registrado")).Html, inversor);
        Exigir(barrera.Fallos.Single() == 1062 && barrera.Llegadas == 2, "F21-rechazo-real-indice-tras-dos-prevalidaciones-" + inversor);
        Exigir(await contexto.Nodos.CountAsync() == antes + 1 && await contexto.Inversores.CountAsync() == antes + 1
            && await contexto.Sensores.CountAsync() == antes + 1, "F19-F20-carrera-sin-parciales-" + inversor);
    }

    private static void ErrorCampo(string html, bool inversor)
    {
        var campo = inversor ? "inversor" : "sensor";
        var bloque = Regex.Match(html, "<div id=\"serie-" + campo + "-error\">(.*?)</div>", RegexOptions.Singleline).Value;
        Exigir(bloque.Contains("Ya existe un " + campo + " con ese número de serie."), "error-en-campo-" + campo);
    }

    private static HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(Direccion) };
    private static async Task<string> Pagina(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta); respuesta.EnsureSuccessStatusCode();
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }
    private static async Task<Guid> CrearParque(HttpClient cliente, string nombre)
    {
        var respuesta = await Enviar(cliente, "/parques-solares/nuevo", new() { ["_handler"] = "alta-parque-solar", ["Modelo.Nombre"] = nombre, ["Modelo.Ubicacion"] = "Destino aislado" });
        Exigir(respuesta.Estado == 302 && respuesta.Ubicacion?.EndsWith("?creado=true") == true, "F01-redireccion-real-tras-post");
        var id = Guid.Parse(Regex.Match(respuesta.Ubicacion!, "/parques-solares/([a-f0-9-]+)").Groups[1].Value);
        await using var contexto = Contexto();
        Exigir(await contexto.ParquesSolares.AnyAsync(p => p.ID == id && p.Nombre.Valor == nombre), "F01-id-redireccion-coincide-base");
        return id;
    }
    private static Dictionary<string, string> Datos(string inversor, string sensor) => new()
    {
        ["_handler"] = "alta-nodo", ["Modelo.Nombre"] = "Nodo web", ["Modelo.Latitud"] = "-34.9", ["Modelo.Longitud"] = "-56.2",
        ["Modelo.NombreInversor"] = "Inversor web", ["Modelo.NumeroSerieInversor"] = inversor,
        ["Modelo.NombreSensor"] = "Sensor web", ["Modelo.NumeroSerieSensor"] = sensor, ["Modelo.EndpointSensor"] = ""
    };
    private static async Task<(int Estado, string Html, string? Ubicacion)> Enviar(HttpClient cliente, string ruta, Dictionary<string, string> datos, string? formulario = null)
    {
        formulario ??= await Pagina(cliente, ruta);
        datos["__RequestVerificationToken"] = Regex.Match(formulario, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var respuesta = await cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
        return ((int)respuesta.StatusCode, WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()), respuesta.Headers.Location?.ToString());
    }

    private sealed class BarreraGuardado : SaveChangesInterceptor
    {
        private TaskCompletionSource _liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _llegadas;
        public bool Activa { get; set; }
        public int Llegadas => _llegadas;
        public ConcurrentQueue<int> Fallos { get; } = new();
        public void Iniciar() { _llegadas = 0; Fallos.Clear(); _liberar = new(TaskCreationOptions.RunContinuationsAsynchronously); Activa = true; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Activa) return result;
            if (Interlocked.Increment(ref _llegadas) == 2) _liberar.TrySetResult();
            await _liberar.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Activa && eventData.Exception.GetBaseException() is MySqlException mysql) Fallos.Enqueue(mysql.Number);
            return Task.CompletedTask;
        }
    }
}
