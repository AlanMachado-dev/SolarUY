// Comprobación funcional HTTP con componentes, servicios y MySQL reales.
// Las sesiones auxiliares no acreditan aceptación con el proveedor OIDC externo.
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Components;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;

internal static class VerificarMod003
{
    private const string Direccion = "http://127.0.0.1:8193";
    private static readonly List<string> Resultados = [];

    public static async Task<int> Main(string[] args)
    {
        try { await EjecutarAsync(args.Contains("preview")); return 0; }
        catch (Exception error)
        {
            Console.WriteLine("Fallo depurado: " + error.GetType().Name);
            if (error is ComprobacionException) Console.WriteLine(error.Message);
            return 1;
        }
    }

    private static async Task EjecutarAsync(bool preview)
    {
        Directory.CreateDirectory("artifacts/mod003");
        var raiz = Directory.GetCurrentDirectory();
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(raiz, "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var habitual = conexion.Database;
        var destino = "solaruy_mod003_" + Guid.NewGuid().ToString("N")[..12];
        Exigir(destino != habitual && Regex.IsMatch(destino, "^solaruy_mod003_[a-f0-9]{12}$"), "destino-aislado");
        conexion.Database = "";
        await using (var administrador = new MySqlConnection(conexion.ConnectionString))
        {
            await administrador.OpenAsync();
            await using var comando = administrador.CreateCommand();
            comando.CommandText = $"CREATE DATABASE `{destino}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
            await comando.ExecuteNonQueryAsync();
        }
        conexion.Database = destino;
        await File.WriteAllTextAsync("artifacts/mod003/destino-aislado.json", JsonSerializer.Serialize(new { Base = destino }));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(App).Assembly.FullName,
            ContentRootPath = Path.Combine(raiz, "SolarUY"), EnvironmentName = "Production"
        });
        builder.WebHost.UseUrls(Direccion);
        builder.WebHost.UseSetting(WebHostDefaults.StaticWebAssetsKey,
            Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.runtime.json"));
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        var fallo = new FalloLecturaInterceptor();
        builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddDbContext<SolarUyDbContext>(opciones =>
        {
            PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
            opciones.AddInterceptors(fallo);
        });
        builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
        builder.Services.AddScoped<INodoRepository, NodoRepository>();
        builder.Services.AddScoped<SolarUY.Application.Monitoreo.IMedicionRepository, MedicionRepositorySinLecturas>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opciones =>
        {
            opciones.Cookie.Name = "SolarUY.Mod003.Auxiliar";
            opciones.LoginPath = "/cuenta/iniciar-sesion";
            opciones.AccessDeniedPath = "/cuenta/acceso-denegado";
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery(); app.UseStaticFiles();
        app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.endpoints.json"));
        app.MapGet("/_auxiliar/sesion/{rol}", async (HttpContext contexto, string rol) =>
        {
            if (rol is not ("Administrador" or "Operador")) return Results.BadRequest();
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "Identidad auxiliar mod003"), new Claim(ClaimTypes.Role, rol)
                }, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Redirect("/parques-solares");
        });
        app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);
        await app.StartAsync();
        try
        {
            using var admin = Cliente(); using var operador = Cliente(); using var anonimo = Cliente();
            await admin.GetAsync("/_auxiliar/sesion/Administrador");
            await operador.GetAsync("/_auxiliar/sesion/Operador");
            using var scope = app.Services.CreateScope();
            var contexto = scope.ServiceProvider.GetRequiredService<SolarUyDbContext>();
            await contexto.Database.MigrateAsync();
            Exigir((await Pagina(admin, "/parques-solares")).Contains("No hay parques solares registrados"), "listado-vacio");
            var parque = ParqueSolar.Crear("Parque de prueba mod003", "Ubicación auxiliar");
            for (var indice = 1; indice <= 2; indice++)
                parque.AgregarNodo("Nodo " + indice, -34.9f, -56.2f,
                    Inversor.Crear("Inversor " + indice, "INV-MOD003-" + indice,
                        Sensor.Crear("Sensor " + indice, "SEN-MOD003-" + indice,
                            indice == 1 ? "https://sensor.example/mediciones" : null)));
            var vacio = ParqueSolar.Crear("Parque sin nodos mod003", "Ubicación auxiliar vacía");
            contexto.AddRange(parque, vacio); await contexto.SaveChangesAsync();
            var ruta = $"/parques-solares/{parque.ID:D}";
            var listado = await Pagina(admin, "/parques-solares");
            var tabla = Regex.Match(listado, "<table[\\s\\S]*?</table>").Value;
            await File.WriteAllTextAsync("artifacts/mod003/tabla.html", tabla);
            Exigir(Regex.Matches(tabla, "<th scope=\"col\"[^>]*>").Count == 3
                && Regex.Matches(tabla, "<td\\b[^>]*>").Count == 6, "CA1-tres-columnas-y-celdas");
            Exigir(!Regex.IsMatch(tabla, ">ID<") && !Regex.IsMatch(SoloTexto(tabla), "[a-f0-9]{8}-[a-f0-9-]{27}"), "CA1-sin-ID-como-dato");
            Exigir(tabla.Contains($"href=\"{ruta}\"") && tabla.Contains("Ver detalles de " + parque.Nombre.Valor), "enlace-y-etiqueta-accesible");
            var detalle = await Pagina(admin, ruta);
            Exigir(detalle.Contains(parque.Nombre.Valor), "detalle-corresponde-al-parque");
            Exigir(!Regex.IsMatch(detalle, "<dt\\b[^>]*>\\s*ID\\s*</dt>")
                && !Regex.IsMatch(SoloTexto(detalle), "[a-f0-9]{8}-[a-f0-9-]{27}"), "CA2-sin-etiquetas-ni-valores-ID");
            foreach (var valor in new[] { parque.Nombre.Valor, parque.Ubicacion, "0 MW", "Latitud", "Longitud", "Cantidad de paneles", "Sin endpoint", "https://sensor.example/mediciones" })
                Exigir(detalle.Contains(valor), "conserva-" + valor);
            foreach (var nodo in parque.Nodos)
            {
                foreach (var valor in new[] { nodo.Nombre, nodo.Inversor.Nombre, nodo.Inversor.NumeroSerie,
                    nodo.Inversor.Sensor.Nombre, nodo.Inversor.Sensor.NumeroSerie,
                    nodo.Latitud.ToString(CultureInfo.CurrentCulture), nodo.Longitud.ToString(CultureInfo.CurrentCulture) })
                    Exigir(detalle.Contains(valor), "conserva-dato-de-nodo");
                Exigir(detalle.Contains($"aria-labelledby=\"nodo-{nodo.ID:D}\"")
                    && Regex.IsMatch(detalle, $"id=\"nodo-{nodo.ID:D}\"[^>]*>{Regex.Escape(nodo.Nombre)}</h3>"), "asociacion-accesible-de-nodo");
            }
            Exigir(detalle.Contains("href=\"/parques-solares\"")
                && detalle.Contains($"href=\"{ruta}/nodos/nuevo\""), "regreso-y-alta-administrador");
            Exigir(!(await Pagina(operador, ruta)).Contains("+ Agregar nodo"), "operador-lee-sin-alta");
            Exigir((await operador.GetAsync(ruta + "/nodos/nuevo")).StatusCode == HttpStatusCode.Redirect, "alta-protegida-por-rol");
            foreach (var protegida in new[] { "/parques-solares", ruta })
            {
                using var respuesta = await anonimo.GetAsync(protegida);
                Exigir(respuesta.StatusCode == HttpStatusCode.Redirect
                    && respuesta.Headers.Location!.ToString().Contains("/cuenta/iniciar-sesion"), "anonimo-requiere-autenticacion");
            }
            Exigir((await Pagina(admin, $"/parques-solares/{vacio.ID:D}")).Contains("El parque solar todavía no tiene nodos."), "parque-sin-nodos");
            var inexistente = $"/parques-solares/{Guid.NewGuid():D}";
            Exigir((await Pagina(admin, inexistente)).Contains("No se encontró el parque solar"), "parque-no-encontrado");
            var formulario = await Pagina(admin, "/parques-solares/nuevo");
            var token = Regex.Match(formulario, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            using var alta = await admin.PostAsync("/parques-solares/nuevo", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["_handler"] = "alta-parque-solar",
                ["Modelo.Nombre"] = "Parque creado mod003", ["Modelo.Ubicacion"] = "Alta aislada"
            }));
            Exigir(alta.StatusCode == HttpStatusCode.Redirect && alta.Headers.Location is not null, "alta-real-en-base-aislada");
            var destinoAlta = alta.Headers.Location!.ToString();
            Exigir(destinoAlta.EndsWith("?creado=true") && (await Pagina(admin, destinoAlta))
                .Contains("El parque solar se creó correctamente."), "mensaje-creacion-tras-redireccion");
            Exigir(!(await Pagina(admin, ruta)).Contains("El parque solar se creó correctamente.")
                && !(await Pagina(admin, inexistente + "?creado=true")).Contains("El parque solar se creó correctamente."), "mensaje-solo-cuando-corresponde");
            fallo.Activo = true;
            try
            {
                foreach (var pagina in new[] { "/parques-solares", ruta })
                {
                    var error = await Pagina(admin, pagina);
                    Exigir(error.Contains("No se pudo cargar la información. Intentá nuevamente")
                        && error.Contains($"href=\"{pagina}\"") && error.Contains(">Recargar</a>"), "fallo-controlado-y-recarga");
                }
            }
            finally { fallo.Activo = false; }
            Exigir((await Pagina(admin, ruta)).Contains(parque.Nombre.Valor)
                && (await Pagina(admin, "/parques-solares")).Contains("tabla-parques"), "recuperacion-tras-fallo");
            await File.WriteAllTextAsync("artifacts/mod003/funcional.json", JsonSerializer.Serialize(new
            {
                correcto = true, destino, ruta, rutaVacio = $"/parques-solares/{vacio.ID:D}",
                comprobaciones = Resultados, alcance = "HTTP SSR y MySQL reales; sesiones auxiliares, fallo de lectura controlado; no aceptación OIDC externa"
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Comprobaciones correctas: {Resultados.Count}. Host auxiliar: {Direccion}");
            if (preview) await app.WaitForShutdownAsync();
        }
        finally { await app.StopAsync(); }
    }

    private static HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(Direccion) };
    private static async Task<string> Pagina(HttpClient cliente, string ruta) => WebUtility.HtmlDecode(await cliente.GetStringAsync(ruta));
    private static string SoloTexto(string html) => Regex.Replace(html, "<[^>]+>", " ");
    private static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new ComprobacionException(criterio);
        Resultados.Add(criterio); Console.WriteLine("OK " + criterio);
    }
    private sealed class ComprobacionException(string criterio) : Exception(criterio);
    private sealed class FalloLecturaInterceptor : DbCommandInterceptor
    {
        public bool Activo { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => Activo ? throw new InvalidOperationException("Fallo auxiliar mod003") : ValueTask.FromResult(result);
    }
}
