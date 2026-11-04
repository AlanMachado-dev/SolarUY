// Integración MySQL + servicios + HTTP SSR reales con identidades auxiliares.
// No acredita aceptación con cuentas externas OIDC. Nunca imprime conexiones ni cookies.
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.Paneles;
using SolarUY.Application.Paneles.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Components;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;

internal static class Program
{
    private const string Carpeta = "artifacts/mod004";
    private const string Direccion = "http://127.0.0.1:8194";
    private static readonly List<string> Resultados = [];

    public static async Task<int> Main(string[] args)
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "SolarUY.slnx"))) raiz = raiz.Parent;
        if (raiz is null) return 1;
        Directory.SetCurrentDirectory(raiz.FullName);
        Directory.CreateDirectory(Carpeta);
        try { await EjecutarAsync(args); return 0; }
        catch (Exception error)
        {
            Console.WriteLine("FALLO " + error.GetBaseException().GetType().Name
                + (error is CriterioFallidoException ? ": " + error.Message : ""));
            return 1;
        }
        finally
        {
            if (!args.Contains("preview") && !args.Contains("limpiar"))
                await File.WriteAllLinesAsync(Carpeta + "/funcional.txt", Resultados);
        }
    }

    private static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new CriterioFallidoException(criterio);
        Resultados.Add("OK " + criterio); Console.WriteLine("OK " + criterio);
    }

    private static async Task EjecutarAsync(string[] args)
    {
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables().Build();
        var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var destino = JsonDocument.Parse(await File.ReadAllTextAsync(Carpeta + "/destino-aislado.json")).RootElement.GetProperty("Base").GetString()!;
        Exigir(Regex.IsMatch(destino, "^solaruy_mod004_[a-f0-9]{12}$") && destino != conexion.Database, "destino-aislado-validado");
        conexion.Database = destino;
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
        await using var db = new SolarUyDbContext(opciones.Options);
        if (args.Contains("limpiar"))
        {
            // Solo el destino exacto propio registrado; nunca enumera ni retira otras bases.
            await db.Database.EnsureDeletedAsync();
            await File.WriteAllTextAsync(Carpeta + "/limpieza.txt", destino + " retirado; configuración habitual intacta.");
            return;
        }
        Guid nodoId, vacioId, otroId, parqueId, otroParqueId;
        if (!args.Contains("preview") && !args.Contains("repetir"))
        {
            var servidor = new MySqlConnectionStringBuilder(conexion.ConnectionString) { Database = "" };
            await using (var sql = new MySqlConnection(servidor.ConnectionString))
            {
                await sql.OpenAsync(); await using var comando = sql.CreateCommand();
                // Sin IF NOT EXISTS: falla en vez de reutilizar una base ajena.
                comando.CommandText = $"CREATE DATABASE `{destino}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
                await comando.ExecuteNonQueryAsync();
            }
            await db.Database.MigrateAsync();
            var parque = ParqueSolar.Crear("Parque mod004 A", "Destino aislado");
            var otro = ParqueSolar.Crear("Parque mod004 B", "Destino aislado");
            var nodo = AgregarNodo(parque, "A", "https://sensor.invalid/solo-texto");
            var vacio = AgregarNodo(parque, "Vacío", null);
            var otroNodo = AgregarNodo(otro, "B", null);
            nodo.AgregarPanel("Panel Z", "Modelo Z", 550, DateTime.UtcNow);
            nodo.AgregarPanel("Panel A", "Modelo A", 450, DateTime.UtcNow);
            db.AddRange(parque, otro); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            (nodoId, vacioId, otroId, parqueId, otroParqueId) = (nodo.ID, vacio.ID, otroNodo.ID, parque.ID, otro.ID);
            await File.WriteAllTextAsync(Carpeta + "/rutas.json", JsonSerializer.Serialize(new { nodoId, vacioId, otroId, parqueId, otroParqueId }));
            await ComprobarServiciosAsync(db, nodoId);
        }
        else
        {
            var rutas = JsonDocument.Parse(await File.ReadAllTextAsync(Carpeta + "/rutas.json")).RootElement;
            nodoId = rutas.GetProperty("nodoId").GetGuid(); vacioId = rutas.GetProperty("vacioId").GetGuid();
            otroId = rutas.GetProperty("otroId").GetGuid(); parqueId = rutas.GetProperty("parqueId").GetGuid();
            otroParqueId = rutas.GetProperty("otroParqueId").GetGuid();
            if (args.Contains("repetir")) await ComprobarServiciosAsync(db, nodoId);
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { ApplicationName = typeof(App).Assembly.FullName, ContentRootPath = Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"), EnvironmentName = "Production" });
        builder.WebHost.UseUrls(Direccion);
        builder.WebHost.UseSetting(WebHostDefaults.StaticWebAssetsKey, Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.runtime.json"));
        builder.WebHost.UseStaticWebAssets(); builder.Logging.ClearProviders();
        var diagnosticos = new DiagnosticosProvider(); builder.Logging.AddProvider(diagnosticos);
        builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddDbContext<SolarUyDbContext>(o => PersistenciaConfiguration.ConfigurarContexto(o, conexion.ConnectionString));
        builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
        builder.Services.AddScoped<INodoRepository, NodoRepository>(); builder.Services.AddScoped<SolarUY.Application.Monitoreo.IMedicionRepository, MedicionRepositorySinLecturas>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddScoped<IPanelRepository, PanelRepository>(); builder.Services.AddScoped<IPanelService, PanelService>();
        builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        { o.Cookie.Name = "SolarUY.Mod004.Auxiliar"; o.LoginPath = "/cuenta/iniciar-sesion"; o.AccessDeniedPath = "/cuenta/acceso-denegado"; });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization(); builder.Services.AddCascadingAuthenticationState();
        await using var app = builder.Build();
        app.UseRequestLocalization(o => o.SetDefaultCulture("es-UY").AddSupportedCultures("es-UY").AddSupportedUICultures("es-UY"));
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery(); app.UseStaticFiles();
        app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.endpoints.json"));
        app.MapGet("/_auxiliar/sesion/{rol}", async (HttpContext contexto, string rol) =>
        {
            if (rol is not ("Administrador" or "Operador" or "Ambos" or "SinRol")) return Results.BadRequest();
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(rol));
            return Results.Redirect($"/parques-solares/{parqueId:D}");
        });
        app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);
        await app.StartAsync();
        try
        {
            if (args.Contains("preview"))
            {
                await File.WriteAllTextAsync(Carpeta + "/preview.pid", Environment.ProcessId.ToString());
                Console.WriteLine("Host auxiliar disponible en " + Direccion); await app.WaitForShutdownAsync();
            }
            else await ComprobarHttpAsync(db, app, diagnosticos, nodoId, vacioId, otroId, parqueId, otroParqueId);
        }
        finally { await app.StopAsync(); }
    }

    private static Nodo AgregarNodo(ParqueSolar parque, string sufijo, string? endpoint) =>
        parque.AgregarNodo("Nodo " + sufijo, -34.5f, -56.2f, Inversor.Crear("Inversor " + sufijo, "I-" + sufijo,
            Sensor.Crear("Sensor " + sufijo, "S-" + sufijo, endpoint)));

    private static ClaimsPrincipal Principal(string? rol)
    {
        if (rol is null) return new(new ClaimsIdentity());
        var claims = new List<Claim> { new(ClaimTypes.Name, "Identidad auxiliar mod004") };
        foreach (var nombre in rol == "Ambos" ? new[] { "Administrador", "Operador" } : new[] { rol }) claims.Add(new(ClaimTypes.Role, nombre));
        return new(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static async Task ComprobarServiciosAsync(SolarUyDbContext db, Guid nodoId)
    {
        var nodos = ContadorRepository.Crear<INodoRepository>(new NodoRepository(db));
        var parques = ContadorRepository.Crear<IParqueSolarRepository>(new ParqueSolarRepository(db));
        var paneles = ContadorRepository.Crear<IPanelRepository>(new PanelRepository(db));
        foreach (var rol in new string?[] { null, "SinRol", "Operador", "Administrador", "Ambos" })
        {
            var usuario = new UsuarioActualService(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = Principal(rol) } });
            var servicio = new NodoService(nodos, parques, usuario, new MedicionRepositorySinLecturas());
            var antes = ContadorRepository.Llamadas;
            var detalle = await servicio.ObtenerPorIdAsync(nodoId);
            if (rol is null or "SinRol")
                Exigir(detalle.Codigo == (rol is null ? CodigoConsultaNodo.AutenticacionRequerida : CodigoConsultaNodo.AccesoDenegado)
                    && detalle.Nodo is null && detalle.Paneles.Count == 0 && antes == ContadorRepository.Llamadas, "VAL13-servicio-rechaza-antes-repositorios-" + (rol ?? "Anonimo"));
            else
            {
                Exigir(detalle.Codigo == CodigoConsultaNodo.Exitoso && detalle.Nodo?.ID == nodoId
                    && detalle.Paneles.Select(p => p.Nombre).SequenceEqual(new[] { "Panel A", "Panel Z" }), "servicio-lectura-completa-orden-" + rol);
                Exigir((await servicio.ObtenerPorIdAsync(Guid.Empty)).Codigo == CodigoConsultaNodo.NoEncontrado
                    && (await servicio.ObtenerPorIdAsync(Guid.NewGuid())).Codigo == CodigoConsultaNodo.NoEncontrado, "servicio-identificadores-ausentes-" + rol);
            }
            if (rol is null or "SinRol" or "Operador")
            {
                antes = ContadorRepository.Llamadas;
                var alta = await new PanelService(nodos, paneles, usuario, TimeProvider.System).CrearAsync(nodoId,
                    new CrearPanelDto { Nombre = "No autorizado", Modelo = "Modelo", PotenciaNominalW = 500 });
                Exigir(!alta.EsExitoso && alta.Errores.Single().Codigo == (rol is null ? CodigoErrorAltaPanel.AutenticacionRequerida : CodigoErrorAltaPanel.AccesoDenegado)
                    && antes == ContadorRepository.Llamadas, "VAL16-alta-panel-rechaza-antes-repositorios-" + (rol ?? "Anonimo"));
                var altaNodo = await servicio.CrearAsync(Guid.NewGuid(), new CrearNodoDto());
                Exigir(!altaNodo.EsExitoso && antes == ContadorRepository.Llamadas, "servicio-alta-nodo-sigue-administrativa-" + (rol ?? "Anonimo"));
            }
        }
    }

    private static HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(Direccion) };
    private static async Task<string> HtmlAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta); Exigir(respuesta.StatusCode == HttpStatusCode.OK, "HTTP-GET-correcto");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }
    private static string Contenido(string html) => Regex.Match(html, "<main[^>]*>(.*?)</main>", RegexOptions.Singleline).Groups[1].Value;
    private static string Token(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task ComprobarHttpAsync(SolarUyDbContext db, WebApplication app, DiagnosticosProvider diagnosticos,
        Guid nodoId, Guid vacioId, Guid otroId, Guid parqueId, Guid otroParqueId)
    {
        using var admin = Cliente(); using var operador = Cliente(); using var ambos = Cliente(); using var sinRol = Cliente(); using var anonimo = Cliente();
        foreach (var par in new[] { (admin, "Administrador"), (operador, "Operador"), (ambos, "Ambos"), (sinRol, "SinRol") }) await par.Item1.GetAsync("/_auxiliar/sesion/" + par.Item2);
        var ruta = $"/nodos/{nodoId:D}"; var altaRuta = ruta + "/paneles/nuevo"; var parqueRuta = $"/parques-solares/{parqueId:D}";
        var cantidadAntes = await db.Paneles.CountAsync();
        var operadorHtml = await HtmlAsync(operador, ruta); var adminHtml = await HtmlAsync(admin, ruta);
        var datosOperador = Regex.Match(operadorHtml, "<div class=\"detalle-nodo\"[^>]*>(.*?)<div class=\"d-flex", RegexOptions.Singleline).Value;
        var datosAdmin = Regex.Match(adminHtml, "<div class=\"detalle-nodo\"[^>]*>(.*?)<div class=\"d-flex", RegexOptions.Singleline).Value;
        Exigir(datosOperador.Length > 0 && datosOperador == datosAdmin, "VAL03-05-mismas-secciones-y-datos-ambos-roles");
        foreach (var valor in new[] { "Nodo A", "-34,5", "-56,2", "Inversor A", "I-A", "Sensor A", "S-A", "https://sensor.invalid/solo-texto", "Panel A", "Modelo A", "450 W", "Operativo", "Sin mediciones", "Sin mantenimientos", " UTC" })
            Exigir(operadorHtml.Contains(valor), "dato-visible-" + valor);
        Exigir(operadorHtml.IndexOf(">Panel A<", StringComparison.Ordinal) < operadorHtml.IndexOf(">Panel Z<", StringComparison.Ordinal), "VAL05-orden-paneles");
        var vacioHtml = await HtmlAsync(operador, $"/nodos/{vacioId:D}");
        Exigir(vacioHtml.Contains("Este nodo todavía no tiene paneles solares") && vacioHtml.Contains("Sin endpoint asociado"), "VAL04-06-opcionales-y-lista-vacia");
        Exigir(operadorHtml.Contains("href=\"" + parqueRuta + "\"") && (await HtmlAsync(operador, $"/nodos/{otroId:D}")).Contains($"href=\"/parques-solares/{otroParqueId:D}\""), "VAL07-regreso-parque-real");
        Exigir(!operadorHtml.Contains("+ Agregar Panel Solar") && !operadorHtml.Contains(altaRuta), "VAL08-operador-sin-enlace-alta");
        Exigir(adminHtml.Contains("+ Agregar Panel Solar") && adminHtml.Contains(altaRuta) && (await HtmlAsync(admin, altaRuta)).Contains("Nodo de destino:"), "VAL09-admin-enlace-alta-correcto");
        Exigir((await HtmlAsync(ambos, ruta)).Contains("+ Agregar Panel Solar") && (await HtmlAsync(ambos, altaRuta)).Contains("Nodo de destino:"), "VAL10-ambos-conserva-permisos-admin");
        var parqueHtml = await HtmlAsync(operador, parqueRuta);
        Exigir(parqueHtml.Contains($"href=\"{ruta}\"") && parqueHtml.Contains($"href=\"/nodos/{vacioId:D}\"") && parqueHtml.Contains("stretched-link enlace-nodo") && parqueHtml.Contains("aria-label=\"Ver detalles de Nodo A\""), "CA1-HTTP-enlaces-nodos-y-nombres-accesibles");
        Exigir(!parqueHtml.Contains("+ Agregar nodo") && (await HtmlAsync(admin, parqueRuta)).Contains("+ Agregar nodo"), "VAL18-alta-nodo-visible-solo-admin");
        var sinRolHtml = await HtmlAsync(sinRol, parqueRuta);
        Exigir(sinRolHtml.Contains("Nodo A") && !sinRolHtml.Contains("enlace-nodo"), "VAL12-sin-rol-nombre-sin-enlace");
        foreach (var par in new[] { (anonimo, "iniciar-sesion"), (sinRol, "acceso-denegado") })
        {
            using var rechazo = await par.Item1.GetAsync(ruta);
            Exigir(rechazo.StatusCode == HttpStatusCode.Redirect && rechazo.Headers.Location!.ToString().Contains(par.Item2)
                && !(await rechazo.Content.ReadAsStringAsync()).Contains("Nodo A"), "VAL11-12-detalle-denegado-sin-datos-" + par.Item2);
        }
        // Token válido del mismo Operador: el POST se rechaza por permisos, no por antiforgery ausente.
        var tokenOperador = Token(parqueHtml); Exigir(tokenOperador.Length > 0, "token-auxiliar-operador-presente-no-registrado");
        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var peticion = new HttpRequestMessage(metodo, altaRuta);
            if (metodo == HttpMethod.Post) peticion.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            { ["_handler"] = "alta-panel", ["__RequestVerificationToken"] = tokenOperador, ["Formulario.Nombre"] = "Alta denegada", ["Formulario.Modelo"] = "M", ["Formulario.PotenciaNominalW"] = "500" });
            using var rechazo = await operador.SendAsync(peticion);
            Exigir(rechazo.StatusCode == HttpStatusCode.Redirect && rechazo.Headers.Location!.ToString().Contains("acceso-denegado")
                && !(await rechazo.Content.ReadAsStringAsync()).Contains("nombre-panel"), "VAL14-15-operador-alta-denegada-" + metodo);
        }
        Exigir(await db.Paneles.CountAsync() == cantidadAntes, "VAL15-rechazos-sin-escrituras-reales");
        foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
        {
            var ausente = await HtmlAsync(operador, $"/nodos/{id:D}");
            Exigir(ausente.Contains("No se encontró el nodo") && ausente.Contains("Volver al listado") && !ausente.Contains("Nodo A"), "VAL19-ausentes-con-recuperacion");
        }
        using (var malformado = await operador.GetAsync("/nodos/no-es-guid")) Exigir(malformado.StatusCode == HttpStatusCode.NotFound, "VAL19-ruta-malformada");
        using (var denegado = await operador.GetAsync(parqueRuta + "/nodos/nuevo"))
            Exigir(denegado.StatusCode == HttpStatusCode.Redirect && denegado.Headers.Location!.ToString().Contains("acceso-denegado"), "VAL18-operador-no-obtiene-alta-nodo");
        Exigir((await HtmlAsync(admin, parqueRuta + "/nodos/nuevo")).Contains("alta-nodo"), "VAL18-admin-conserva-alta-nodo");
        await using (var sql = new MySqlConnection(db.Database.GetConnectionString()))
        {
            await sql.OpenAsync(); await using var comando = sql.CreateCommand();
            comando.CommandText = "RENAME TABLE Paneles TO Paneles_mod004_fallo"; await comando.ExecuteNonQueryAsync();
            try
            {
                var fallo = await HtmlAsync(operador, ruta);
                Exigir(fallo.Contains("No se pudo cargar la información") && fallo.Contains("Recargar") && !fallo.Contains("No se encontró el nodo") && !fallo.Contains("Nodo A"), "VAL20-fallo-MySQL-real-sin-datos-con-recarga");
            }
            finally { comando.CommandText = "RENAME TABLE Paneles_mod004_fallo TO Paneles"; await comando.ExecuteNonQueryAsync(); }
        }
        Exigir((await HtmlAsync(operador, ruta)).Contains("Nodo A") && diagnosticos.Codigos.Contains("No se pudo cargar el detalle del nodo. Código: consulta_detalle_nodo."), "VAL20-recuperacion-y-diagnostico-depurado");
        Exigir(!(await HtmlAsync(operador, $"/nodos/{otroId:D}")).Contains("Nodo A"), "VAL21-cambio-nodo-sin-datos-anteriores-SSR");
        Exigir(!Regex.IsMatch(Regex.Replace(Contenido(operadorHtml), "<[^>]+>", " "), "[a-f0-9]{8}-[a-f0-9-]{27}"), "sin-ids-visibles-en-detalle");
        Exigir(await db.Paneles.CountAsync() == cantidadAntes, "consultas-sin-escrituras");
        var formulario = await HtmlAsync(admin, altaRuta);
        using (var alta = await admin.PostAsync(altaRuta, new FormUrlEncodedContent(new Dictionary<string, string>
        { ["_handler"] = "alta-panel", ["__RequestVerificationToken"] = Token(formulario), ["Formulario.Nombre"] = "Panel creado mod004", ["Formulario.Modelo"] = "Modelo HTTP", ["Formulario.PotenciaNominalW"] = "600" })))
            Exigir(alta.StatusCode == HttpStatusCode.Redirect && alta.Headers.Location!.ToString().EndsWith(ruta + "?panelRegistrado=true", StringComparison.Ordinal), "VAL17-admin-alta-HTTP-retorna-detalle");
        Exigir(await db.Paneles.CountAsync() == cantidadAntes + 1 && (await HtmlAsync(operador, ruta)).Contains("Panel creado mod004"), "VAL17-panel-persistido-visible-operador");
        await File.WriteAllLinesAsync(Carpeta + "/diagnosticos.txt", diagnosticos.Codigos);
    }
}

// Decorador de observación: delega en repositorios reales; cuenta incluso llamadas sin SQL.
public class ContadorRepository : DispatchProxy
{
    private object _destino = null!;
    public static int Llamadas { get; private set; }
    public static T Crear<T>(T destino) where T : class
    { var proxy = Create<T, ContadorRepository>(); ((ContadorRepository)(object)proxy)._destino = destino; return proxy; }
    protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos)
    {
        Llamadas++;
        try { return metodo!.Invoke(_destino, argumentos); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
}
internal sealed class DiagnosticosProvider : ILoggerProvider
{
    internal List<string> Codigos { get; } = [];
    public ILogger CreateLogger(string categoria) => new DiagnosticoLogger(this, categoria);
    public void Dispose() { }
    private sealed class DiagnosticoLogger(DiagnosticosProvider proveedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => categoria == "SolarUY.Presentation.Pages.DetalleNodo" && nivel == LogLevel.Warning;
        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? error, Func<TState, Exception?, string> formato)
        { if (IsEnabled(nivel)) proveedor.Codigos.Add(formato(estado, null)); }
    }
}
internal sealed class CriterioFallidoException(string criterio) : Exception(criterio);
