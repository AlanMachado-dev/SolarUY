// Auxiliar funcional con MySQL y SSR reales; las identidades locales no acreditan OIDC externo.
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
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
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Components;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using MySql.Data.MySqlClient;

internal static class VerificarDetalleNodo
{
    private const string Direccion = "http://127.0.0.1:8195";
    private static readonly List<string> Resultados = [];

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] != "preview")
                return await ControlDestino.EjecutarAsync(args[0]);
            await EjecutarAsync(args.Contains("preview"));
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("Fallo depurado: " + error.GetType().Name);
            if (error is CriterioFallidoException) Console.WriteLine(error.Message);
            return 1;
        }
    }

    private static async Task EjecutarAsync(bool preview)
    {
        Directory.CreateDirectory("artifacts/req005");
        var raiz = Directory.GetCurrentDirectory();
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(raiz, "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var habitual = conexion.Database;
        var destino = "solaruy_req005_" + Guid.NewGuid().ToString("N")[..12];
        Exigir(destino != habitual && Regex.IsMatch(destino, "^solaruy_req005_[a-f0-9]{12}$"), "destino-aislado");
        conexion.Database = "";
        await using (var administrador = new MySqlConnection(conexion.ConnectionString))
        {
            await administrador.OpenAsync();
            await using var comando = administrador.CreateCommand();
            comando.CommandText = $"CREATE DATABASE `{destino}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
            await comando.ExecuteNonQueryAsync();
        }
        conexion.Database = destino;
        await File.WriteAllTextAsync("artifacts/req005/destino-aislado.json", JsonSerializer.Serialize(new { Base = destino }));
        await File.AppendAllTextAsync("artifacts/req005/destinos-creados.txt", destino + Environment.NewLine);

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
        var diagnosticos = new DiagnosticoProvider();
        builder.Logging.AddProvider(diagnosticos);
        var lectura = new LecturaInterceptor();
        builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddDbContext<SolarUyDbContext>(opciones =>
        {
            PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
            opciones.AddInterceptors(lectura);
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
            opciones.Cookie.Name = "SolarUY.Req005.Auxiliar";
            opciones.LoginPath = "/cuenta/iniciar-sesion";
            opciones.AccessDeniedPath = "/cuenta/acceso-denegado";
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        await using var app = builder.Build();
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseStaticFiles();
        app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.endpoints.json"));
        app.MapGet("/_auxiliar/sesion/{rol}", async (HttpContext contexto, string rol) =>
        {
            if (rol is not ("Administrador" or "Operador")) return Results.BadRequest();
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "Identidad auxiliar req005"), new Claim(ClaimTypes.Role, rol)
                }, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Redirect("/parques-solares");
        });
        var solicitudesSensor = 0;
        app.MapGet("/_auxiliar/sensor", () => { Interlocked.Increment(ref solicitudesSensor); return Results.NoContent(); });
        app.MapGet("/_auxiliar/estado", () => Results.Json(new { solicitudesSensor }));
        app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);
        await app.StartAsync();
        try
        {
            using var scope = app.Services.CreateScope();
            var contexto = scope.ServiceProvider.GetRequiredService<SolarUyDbContext>();
            await contexto.Database.MigrateAsync();
            var parque = ParqueSolar.Crear("Parque req005 A", "Destino aislado A");
            var otro = ParqueSolar.Crear("Parque req005 B", "Destino aislado B");
            var vacio = ParqueSolar.Crear("Parque req005 vacío", "Destino aislado sin nodos");
            var largo = new string('X', 180);
            var nodo = parque.AgregarNodo("Nodo req005 A " + largo, 0, 0,
                Inversor.Crear("Inversor A " + largo, "INV-A-" + largo,
                    Sensor.Crear("Sensor A " + largo, "SEN-A-" + largo,
                        Direccion + "/_auxiliar/sensor?valor=<script>alert('dato')</script>&largo=" + largo)));
            var sinEndpoint = otro.AgregarNodo("Nodo req005 B", -34.9f, -56.2f,
                Inversor.Crear("Inversor B", "INV-B", Sensor.Crear("Sensor B", "SEN-B")));
            var segundo = parque.AgregarNodo("Nodo req005 C", 10.25f, 20.5f,
                Inversor.Crear("Inversor C", "INV-C", Sensor.Crear("Sensor C", "SEN-C")));
            contexto.AddRange(parque, otro, vacio);
            await contexto.SaveChangesAsync();
            contexto.ChangeTracker.Clear();
            await File.WriteAllTextAsync("artifacts/req005/rutas.json", JsonSerializer.Serialize(new
            {
                parque = $"/parques-solares/{parque.ID:D}", otro = $"/parques-solares/{otro.ID:D}",
                vacio = $"/parques-solares/{vacio.ID:D}", nodo = $"/nodos/{nodo.ID:D}",
                sinEndpoint = $"/nodos/{sinEndpoint.ID:D}", segundo = $"/nodos/{segundo.ID:D}"
            }, new JsonSerializerOptions { WriteIndented = true }));

            await VerificarServicioAsync(contexto, nodo, sinEndpoint, lectura);
            using var admin = Cliente(); using var operador = Cliente(); using var anonimo = Cliente();
            await admin.GetAsync("/_auxiliar/sesion/Administrador");
            await operador.GetAsync("/_auxiliar/sesion/Operador");
            var antes = await EstadoAsync(contexto);
            foreach (var esperado in new[] { nodo, sinEndpoint })
            {
                var ruta = $"/nodos/{esperado.ID:D}";
                var html = await admin.GetStringAsync(ruta);
                var texto = WebUtility.HtmlDecode(html);
                foreach (var valor in new[] { esperado.Nombre, esperado.Inversor.Nombre, esperado.Inversor.NumeroSerie,
                    esperado.Inversor.Sensor.Nombre, esperado.Inversor.Sensor.NumeroSerie,
                    esperado.Inversor.Sensor.Endpoint ?? "Sin endpoint asociado" })
                    Exigir(texto.Contains(valor), "detalle-dato-persistido");
                Exigir(Regex.Matches(html, "<section aria-labelledby=\"(?:nodo|inversor|sensor)-titulo\"").Count == 3,
                    "detalle-tres-secciones");
                foreach (var coordenada in new[] { ("Latitud", esperado.Latitud), ("Longitud", esperado.Longitud) })
                    Exigir(Regex.IsMatch(texto, "<dt[^>]*>" + coordenada.Item1 + "</dt>\\s*<dd[^>]*>"
                        + Regex.Escape(coordenada.Item2.ToString(CultureInfo.CurrentCulture)) + "</dd>"), "coordenadas-incluido-cero");
                Exigir(html.Contains($"href=\"/parques-solares/{esperado.Parque.ID:D}\"")
                    && texto.Contains(">Volver al parque</a>"), "regreso-parque-real-desde-url-directa");
                Exigir(Regex.IsMatch(html, "<button[^>]*type=\"button\"[^>]*disabled[^>]*>\\+ Agregar Panel Solar</button>"), "paneles-reserva-deshabilitada");
                Exigir(!Regex.IsMatch(Regex.Replace(texto, "<[^>]+>", " "), "[a-f0-9]{8}-[a-f0-9-]{27}"), "sin-ids-visibles");
                Exigir(!html.Contains("<script>alert('dato')</script>") && !html.Contains("href=\"" + Direccion + "/_auxiliar/sensor"), "endpoint-codificado-sin-enlace");
            }
            var detalleParque = WebUtility.HtmlDecode(await admin.GetStringAsync($"/parques-solares/{parque.ID:D}"));
            foreach (var seleccionado in parque.Nodos)
                Exigir(detalleParque.Contains($"href=\"/nodos/{seleccionado.ID:D}\"")
                    && detalleParque.Contains("Ver detalles de " + seleccionado.Nombre), "tarjeta-enlace-real-accesible");
            var vistaOperador = await operador.GetStringAsync($"/parques-solares/{parque.ID:D}");
            Exigir(!vistaOperador.Contains("enlace-nodo") && !WebUtility.HtmlDecode(vistaOperador).Contains("+ Agregar nodo")
                && vistaOperador.Contains("nodo-detalle"), "operador-conserva-parque-sin-enlaces-ni-alta");
            foreach (var cliente in new[] { operador, anonimo })
            {
                using var respuesta = await cliente.GetAsync($"/nodos/{nodo.ID:D}");
                Exigir(respuesta.StatusCode == HttpStatusCode.Redirect && respuesta.Headers.Location!.ToString()
                    .Contains(cliente == operador ? "/cuenta/acceso-denegado" : "/cuenta/iniciar-sesion"), "permisos-http-sesion-auxiliar");
                Exigir(!(await respuesta.Content.ReadAsStringAsync()).Contains(nodo.Nombre), "rechazo-http-sin-datos");
            }
            foreach (var id in new[] { Guid.NewGuid(), Guid.Empty })
            {
                var texto = WebUtility.HtmlDecode(await admin.GetStringAsync($"/nodos/{id:D}"));
                Exigir(texto.Contains("No se encontró el nodo") && texto.Contains(">Volver al listado</a>")
                    && !texto.Contains("Volver al parque") && !texto.Contains("Agregar Panel Solar")
                    && !texto.Contains("nodo-titulo"), "no-encontrado-sin-acciones-ni-detalle");
            }
            using (var malformada = await admin.GetAsync("/nodos/no-es-guid"))
                Exigir(malformada.StatusCode == HttpStatusCode.NotFound, "guid-malformado-ruta-no-encontrada");
            lectura.Fallar = true;
            try
            {
                var texto = WebUtility.HtmlDecode(await admin.GetStringAsync($"/nodos/{nodo.ID:D}"));
                Exigir(texto.Contains("No se pudo cargar la información. Intentá nuevamente")
                    && texto.Contains($"href=\"/nodos/{nodo.ID:D}\"") && texto.Contains(">Recargar</a>")
                    && !texto.Contains("nodo-titulo"), "fallo-controlado-generico-y-recarga");
                Exigir(diagnosticos.Mensajes.SequenceEqual(new[] { "No se pudo cargar el detalle del nodo. Código: consulta_detalle_nodo." }), "diagnostico-depurado");
            }
            finally { lectura.Fallar = false; }
            Exigir(WebUtility.HtmlDecode(await admin.GetStringAsync($"/nodos/{sinEndpoint.ID:D}")).Contains(sinEndpoint.Nombre), "recuperacion-y-otro-detalle");
            Exigir(antes == await EstadoAsync(contexto) && solicitudesSensor == 0, "consulta-sin-escrituras-ni-solicitudes-sensor");
            Exigir((await admin.GetStringAsync("/parques-solares")).Contains("tabla-parques"), "regresion-listado");
            Exigir(WebUtility.HtmlDecode(await admin.GetStringAsync($"/parques-solares/{vacio.ID:D}"))
                .Contains("El parque solar todavía no tiene nodos."), "regresion-parque-vacio");
            await VerificarAltaAsync(admin, otro.ID, contexto);
            await File.WriteAllTextAsync("artifacts/req005/funcional.json", JsonSerializer.Serialize(new
            {
                correcto = true, destino, comprobaciones = Resultados,
                alcance = "MySQL, HTTP SSR y componentes reales; sesiones auxiliares; no aceptación OIDC externa"
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Comprobaciones correctas: {Resultados.Count}. Host auxiliar: {Direccion}");
            if (preview) await app.WaitForShutdownAsync();
        }
        finally { await app.StopAsync(); }
    }

    private static async Task VerificarServicioAsync(SolarUyDbContext contexto, Nodo esperado, Nodo sinEndpoint,
        LecturaInterceptor lectura)
    {
        var acceso = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var servicio = new NodoService(new NodoRepository(contexto), new ParqueSolarRepository(contexto), new UsuarioActualService(acceso), new MedicionRepositorySinLecturas());
        foreach (var rol in new string?[] { null, "Operador", "administrador" })
        {
            acceso.HttpContext.User = rol is null ? new(new ClaimsIdentity())
                : new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, rol) }, "Auxiliar"));
            var antes = lectura.Cantidad;
            foreach (var id in new[] { esperado.ID, Guid.Empty })
            {
                var resultado = await servicio.ObtenerPorIdAsync(id);
                Exigir(resultado.Codigo == (rol is null ? CodigoConsultaNodo.AutenticacionRequerida : CodigoConsultaNodo.AccesoDenegado)
                    && resultado.Nodo is null && lectura.Cantidad == antes, "ACE-10-rechazo-antes-de-persistencia-auxiliar");
            }
        }
        acceso.HttpContext.User = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Administrador") }, "Auxiliar"));
        var parqueSeguido = await contexto.ParquesSolares.FirstAsync();
        var entradas = contexto.ChangeTracker.Entries().Count();
        var resultadoExitoso = await servicio.ObtenerPorIdAsync(esperado.ID);
        Exigir(resultadoExitoso.Codigo == CodigoConsultaNodo.Exitoso && resultadoExitoso.Nodo is { } dto
            && dto.ParqueId == esperado.Parque.ID && dto.ID == esperado.ID && dto.Nombre == esperado.Nombre
            && dto.Inversor.Sensor.Endpoint == esperado.Inversor.Sensor.Endpoint
            && contexto.ChangeTracker.Entries().Count() == entradas
            && contexto.Entry(parqueSeguido).State == EntityState.Unchanged, "consulta-grafo-completo-sin-alterar-seguimiento");
        foreach (var id in new[] { Guid.NewGuid(), Guid.Empty })
        {
            var resultado = await servicio.ObtenerPorIdAsync(id);
            Exigir(resultado.Codigo == CodigoConsultaNodo.NoEncontrado && resultado.Nodo is null, "servicio-no-encontrado");
        }
        Exigir((await servicio.ObtenerPorIdAsync(sinEndpoint.ID)).Nodo!.Inversor.Sensor.Endpoint is null, "servicio-endpoint-opcional");
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        try { await servicio.ObtenerPorIdAsync(esperado.ID, cancelacion.Token); throw new CriterioFallidoException("cancelacion-no-propagada"); }
        catch (OperationCanceledException) { Exigir(true, "cancelacion-propagada-a-ef"); }
        lectura.Fallar = true;
        try { await servicio.ObtenerPorIdAsync(esperado.ID); throw new CriterioFallidoException("fallo-no-propagado"); }
        catch (FalloControladoException) { Exigir(true, "fallo-tecnico-propagado"); }
        finally { lectura.Fallar = false; }
        contexto.ChangeTracker.Clear();
    }

    private static async Task VerificarAltaAsync(HttpClient admin, Guid parqueId, SolarUyDbContext contexto)
    {
        var ruta = $"/parques-solares/{parqueId:D}/nodos/nuevo";
        var formulario = await admin.GetStringAsync(ruta);
        var token = Regex.Match(formulario, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var alta = await admin.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["_handler"] = "alta-nodo", ["Modelo.Nombre"] = "Nodo alta regresión req005",
            ["Modelo.Latitud"] = "0", ["Modelo.Longitud"] = "0", ["Modelo.NombreInversor"] = "Inversor alta",
            ["Modelo.NumeroSerieInversor"] = "INV-ALTA-REQ005", ["Modelo.NombreSensor"] = "Sensor alta", ["Modelo.NumeroSerieSensor"] = "SEN-ALTA-REQ005"
        }));
        Exigir(alta.IsSuccessStatusCode && WebUtility.HtmlDecode(await alta.Content.ReadAsStringAsync()).Contains("Nodo registrado")
            && await contexto.Nodos.AsNoTracking().AnyAsync(nodo => nodo.Nombre == "Nodo alta regresión req005"), "regresion-alta-http-y-persistencia-aislada");
    }

    private static async Task<string> EstadoAsync(SolarUyDbContext contexto)
    {
        var estado = new
        {
            parques = await contexto.ParquesSolares.AsNoTracking().OrderBy(p => p.ID).Select(p => new { p.ID, p.Nombre.Valor, p.Ubicacion }).ToListAsync(),
            nodos = await contexto.Nodos.AsNoTracking().OrderBy(n => n.ID).Select(n => new { n.ID, n.Nombre, n.Latitud, n.Longitud, Parque = n.Parque.ID }).ToListAsync(),
            inversores = await contexto.Inversores.AsNoTracking().OrderBy(i => i.ID).Select(i => new { i.ID, i.Nombre, i.NumeroSerie }).ToListAsync(),
            sensores = await contexto.Sensores.AsNoTracking().OrderBy(s => s.ID).Select(s => new { s.ID, s.Nombre, s.NumeroSerie, s.Endpoint }).ToListAsync()
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(estado)));
    }

    private static HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(Direccion) };
    private static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new CriterioFallidoException(criterio);
        Resultados.Add(criterio);
        Console.WriteLine("OK " + criterio);
    }
    private sealed class CriterioFallidoException(string criterio) : Exception(criterio);
    private sealed class FalloControladoException : Exception;
    private sealed class LecturaInterceptor : DbCommandInterceptor
    {
        public bool Fallar { get; set; }
        public int Cantidad { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Cantidad++;
            return Fallar ? throw new FalloControladoException() : ValueTask.FromResult(result);
        }
    }
    private sealed class DiagnosticoProvider : ILoggerProvider
    {
        public List<string> Mensajes { get; } = [];
        public ILogger CreateLogger(string categoria) => new DiagnosticoLogger(categoria, Mensajes);
        public void Dispose() { }
        private sealed class DiagnosticoLogger(string categoria, List<string> mensajes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel nivel) => categoria == "SolarUY.Presentation.Pages.DetalleNodo" && nivel >= LogLevel.Warning;
            public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? error, Func<TState, Exception?, string> formatear)
            {
                if (IsEnabled(nivel)) mensajes.Add(formatear(estado, error));
            }
        }
    }
}
