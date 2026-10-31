// Puente exclusivo localhost: utiliza la sesión OIDC que el usuario ya inició.
// No emite identidades. Cookies, tokens y HTML protegido se mantienen en memoria.
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SolarUY.Infrastructure.Persistencia;

var directorio = new DirectoryInfo(AppContext.BaseDirectory);
while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "SolarUY.slnx"))) directorio = directorio.Parent;
if (directorio is null) throw new InvalidOperationException("No se encontró la raíz de SolarUY.");
Directory.SetCurrentDirectory(directorio.FullName);
if (args.Contains("limpiar-anteriores") || args.Contains("limpiar-todos"))
{
    await RetirarDestinosAsync(args.Contains("limpiar-todos"));
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("https://localhost:8197");
builder.Logging.ClearProviders();
builder.Services.AddAntiforgery();
var app = builder.Build();
app.UseAntiforgery();
app.MapGet("/", (HttpContext contexto, IAntiforgery antiforgery) =>
{
    var token = antiforgery.GetAndStoreTokens(contexto).RequestToken!;
    return Results.Content($$"""
        <!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Aceptación req006</title><style>body{font:18px system-ui;max-width:48rem;margin:3rem auto;padding:1rem}button{font:inherit;padding:1rem;margin:1rem 0}a{display:block;margin:1rem 0}</style>
        <h1>Aceptación req006 con sesión real</h1>
        <p>Usa la sesión iniciada por el usuario en SolarUY. Solo escribe en el destino aislado req006.</p>
        <a href="https://localhost:7178/parques-solares">Abrir SolarUY</a>
        <form method="post" action="/administrador"><input type="hidden" name="__RequestVerificationToken" value="{{WebUtility.HtmlEncode(token)}}"><button>Verificar Administrador</button></form>
        <form method="post" action="/sin-rol"><input type="hidden" name="__RequestVerificationToken" value="{{WebUtility.HtmlEncode(token)}}"><button>Verificar usuario sin Administrador</button></form>
        </html>
        """, "text/html");
});
foreach (var rol in new[] { "administrador", "sin-rol" })
    app.MapPost("/" + rol, async (HttpContext contexto, IAntiforgery antiforgery) =>
    {
        try
        {
            await antiforgery.ValidateRequestAsync(contexto);
            using var prueba = new AceptacionReq006(contexto.Request.Headers.Cookie.ToString());
            await prueba.EjecutarAsync(rol);
            return Results.Content("<!doctype html><html lang=es><meta charset=utf-8><h1>Verificación completada</h1><p>" + prueba.Cantidad
                + " comprobaciones aprobadas con la sesión real.</p><a href=/>Volver</a></html>", "text/html");
        }
        catch (CriterioFallidoException error)
        {
            return Results.Content("<!doctype html><html lang=es><meta charset=utf-8><h1>Verificación pendiente</h1><p>" + WebUtility.HtmlEncode(error.Message)
                + "</p><a href=/>Volver</a></html>", "text/html", statusCode: 409);
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync("artifacts/req006/ace-diagnostico.json", JsonSerializer.Serialize(new
            { Tipo = error.GetBaseException().GetType().Name, Origen = error.TargetSite?.DeclaringType?.Name + "." + error.TargetSite?.Name,
                Codigo = error.GetBaseException() is MySqlException mysql ? mysql.Number : 0 }));
            return Results.Content("<!doctype html><html lang=es><meta charset=utf-8><h1>Verificación pendiente</h1><p>Código: aceptacion_req006.</p><a href=/>Volver</a></html>", "text/html", statusCode: 500);
        }
    });
Directory.CreateDirectory("artifacts/req006");
await File.WriteAllTextAsync("artifacts/req006/ace.pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
await app.RunAsync();

static async Task RetirarDestinosAsync(bool incluirActual)
{
    var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
        .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables().Build();
    var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
    var habitual = conexion.Database;
    var actual = JsonDocument.Parse(await File.ReadAllTextAsync("artifacts/req006/destino-aislado.json")).RootElement.GetProperty("Base").GetString()!;
    var anteriores = File.Exists("artifacts/req006/destinos-anteriores.txt") ? await File.ReadAllTextAsync("artifacts/req006/destinos-anteriores.txt") : "";
    var destinos = Regex.Matches(anteriores, "solaruy_req006_[a-f0-9]{12}").Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
    if (incluirActual) destinos.Add(actual);
    else destinos.Remove(actual);
    if (destinos.Any(d => !Regex.IsMatch(d,"^solaruy_req006_[a-f0-9]{12}$") || d == habitual))
        throw new InvalidOperationException("Destino de limpieza no autorizado.");
    conexion.Database = "";
    await using var sql = new MySqlConnection(conexion.ConnectionString); await sql.OpenAsync();
    foreach (var destino in destinos)
    {
        await using var comando = sql.CreateCommand();
        comando.CommandText = $"DROP DATABASE IF EXISTS `{destino}`";
        await comando.ExecuteNonQueryAsync();
        await File.AppendAllTextAsync("artifacts/req006/limpieza.txt", destino + " retirado" + Environment.NewLine);
    }
    Console.WriteLine("Destinos propios retirados: " + destinos.Count);
}

internal sealed class AceptacionReq006 : IDisposable
{
    private const string Carpeta = "artifacts/req006";
    private readonly HttpClient _cliente;
    private readonly List<string> _resultados = [];
    private string _conexion = "";
    public int Cantidad => _resultados.Count;

    internal AceptacionReq006(string cookies)
    {
        _cliente = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { BaseAddress = new Uri("https://localhost:7178") };
        if (!string.IsNullOrEmpty(cookies)) _cliente.DefaultRequestHeaders.Add("Cookie", cookies);
    }

    internal async Task EjecutarAsync(string rol)
    {
        var config = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables().Build();
        var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(config));
        var destino = JsonDocument.Parse(await File.ReadAllTextAsync(Carpeta + "/destino-aislado.json")).RootElement.GetProperty("Base").GetString()!;
        Exigir(Regex.IsMatch(destino, "^solaruy_req006_[a-f0-9]{12}$") && destino != conexion.Database, "destino-aislado-confirmado");
        conexion.Database = destino; _conexion = conexion.ConnectionString;
        var rutas = JsonDocument.Parse(await File.ReadAllTextAsync(Carpeta + "/rutas.json")).RootElement;
        var nodoId = rutas.GetProperty("nodo").GetGuid();
        var segundoId = rutas.GetProperty("segundo").GetGuid();
        var parqueId = rutas.GetProperty("parque").GetGuid();
        try
        {
            if (rol == "administrador") await AdministradorAsync(nodoId, segundoId, parqueId);
            else await SinRolAsync(nodoId, parqueId);
        }
        finally { await File.WriteAllLinesAsync(Carpeta + "/oidc-" + rol + ".txt", _resultados); }
    }

    private SolarUyDbContext Contexto()
    {
        var options = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(options, _conexion);
        return new SolarUyDbContext(options.Options);
    }

    private void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new CriterioFallidoException(criterio);
        _resultados.Add("OK " + criterio);
    }

    private async Task<string> ObtenerAsync(string ruta)
    {
        using var respuesta = await _cliente.GetAsync(ruta);
        Exigir(respuesta.StatusCode == HttpStatusCode.OK, "GET-autorizado");
        return await respuesta.Content.ReadAsStringAsync();
    }

    private static Dictionary<string, string> Campos(string html, string nombre, string potencia = "710") => new()
    {
        ["_handler"] = "alta-panel", ["__RequestVerificationToken"] = Token(html),
        ["Formulario.Nombre"] = nombre, ["Formulario.Modelo"] = "Modelo OIDC", ["Formulario.PotenciaNominalW"] = potencia
    };

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success) throw new CriterioFallidoException("token-de-formulario-ausente");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private async Task AdministradorAsync(Guid nodoId, Guid segundoId, Guid parqueId)
    {
        var ruta = $"/nodos/{nodoId:D}/paneles/nuevo";
        var detalleUrl = $"/nodos/{nodoId:D}";
        var formulario = await ObtenerAsync(ruta);
        Exigir(WebUtility.HtmlDecode(formulario).Contains("Nodo de destino:"), "OIDC-Administrador-real-formulario");
        await using var db = Contexto();
        var cantidadAntes = await db.Paneles.CountAsync();
        foreach (var caso in new[] { ("Nombre", ""), ("Nombre", new string('X',101)), ("Modelo", ""), ("PotenciaNominalW", ""), ("PotenciaNominalW", "0"), ("PotenciaNominalW", "-1") })
        {
            var campos = Campos(formulario, "Validación real"); campos["Formulario." + caso.Item1] = caso.Item2;
            using var respuesta = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(campos));
            var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            Exigir(respuesta.StatusCode == HttpStatusCode.OK && html.Contains("validation-message")
                && html.Contains(caso.Item1 == "Modelo" ? "value=\"Validación real\"" : "value=\"Modelo OIDC\""), "OIDC-validacion-campo-conserva-" + caso.Item1);
        }
        Exigir(await db.Paneles.CountAsync() == cantidadAntes, "OIDC-invalidos-sin-escrituras");
        using (var sinToken = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string,string> { ["_handler"]="alta-panel" })))
            Exigir(sinToken.StatusCode == HttpStatusCode.BadRequest, "OIDC-antifalsificacion");
        var nombre = "Panel OIDC " + Guid.NewGuid().ToString("N")[..8];
        var alta = Campos(formulario, nombre);
        alta["Formulario.ID"] = Guid.NewGuid().ToString(); alta["Formulario.NodoId"] = segundoId.ToString();
        alta["Formulario.Estado"] = "Desactivado"; alta["Formulario.FechaInstalacion"] = "2000-01-01";
        var antes = DateTime.UtcNow.AddSeconds(-1);
        using (var respuesta = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(alta)))
            Exigir(respuesta.StatusCode == HttpStatusCode.Redirect && respuesta.Headers.Location!.ToString().EndsWith(detalleUrl + "?panelRegistrado=true", StringComparison.Ordinal), "OIDC-redireccion-post-mismo-nodo");
        var panel = await db.Paneles.AsNoTracking().Include(p => p.Nodo).SingleAsync(p => p.Nombre.Valor == nombre);
        Exigir(panel.ID != Guid.Empty && panel.ID.ToString() != alta["Formulario.ID"] && panel.Nodo.ID == nodoId
            && panel.Modelo == "Modelo OIDC" && panel.PotenciaNominalW == 710 && panel.Estado.ToString() == "Operativo"
            && panel.FechaInstalacion.Kind == DateTimeKind.Utc && panel.FechaInstalacion.Ticks % TimeSpan.TicksPerSecond == 0
            && panel.FechaInstalacion >= antes && panel.FechaInstalacion <= DateTime.UtcNow, "OIDC-datos-persistidos-servidor-ignora-campos-manipulados");
        var detalle = WebUtility.HtmlDecode(await ObtenerAsync(detalleUrl + "?panelRegistrado=true"));
        Exigir(detalle.Contains("Panel solar registrado") && detalle.Contains(nombre) && detalle.Contains("710 W") && detalle.Contains(" UTC")
            && detalle.Contains("Operativo") && detalle.Contains("Sin mediciones") && detalle.Contains("Sin mantenimientos"), "OIDC-lista-completa-derivados");
        await ObtenerAsync(detalleUrl + "?panelRegistrado=true");
        Exigir(await db.Paneles.CountAsync() == cantidadAntes + 1, "OIDC-recarga-sin-reenvio");
        using (var duplicado = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(Campos(formulario, " " + nombre.ToUpperInvariant() + " "))))
            Exigir(WebUtility.HtmlDecode(await duplicado.Content.ReadAsStringAsync()).Contains("Ya existe un panel solar con ese nombre en este nodo."), "OIDC-duplicado-mayusculas-espacios");
        var otraRuta = $"/nodos/{segundoId:D}/paneles/nuevo";
        using (var repetido = await _cliente.PostAsync(otraRuta, new FormUrlEncodedContent(Campos(await ObtenerAsync(otraRuta), nombre))))
            Exigir(repetido.StatusCode == HttpStatusCode.Redirect, "OIDC-mismo-nombre-otro-nodo");
        foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
        {
            var ausente = WebUtility.HtmlDecode(await ObtenerAsync($"/nodos/{id:D}/paneles/nuevo"));
            Exigir(ausente.Contains("No se encontró el nodo") && !ausente.Contains("id=\"nombre-panel\""), "OIDC-ausente-sin-formulario-ni-datos");
        }
        using (var malformado = await _cliente.GetAsync("/nodos/invalido/paneles/nuevo"))
            Exigir(malformado.StatusCode == HttpStatusCode.NotFound, "OIDC-guid-malformado");
        using (var anonimo = new HttpClient(new HttpClientHandler { AllowAutoRedirect=false, UseCookies=false }) { BaseAddress=_cliente.BaseAddress })
        {
            foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Post })
            {
                using var peticion = new HttpRequestMessage(metodo, ruta);
                if (metodo == HttpMethod.Post) peticion.Content = new FormUrlEncodedContent(Campos(formulario, "Anónimo"));
                using var rechazo = await anonimo.SendAsync(peticion);
                Exigir(rechazo.StatusCode == HttpStatusCode.Redirect && rechazo.Headers.Location!.ToString().Contains("iniciar-sesion"), "OIDC-anonimo-get-post-rechazados");
            }
        }
        await FallosAsync(ruta, detalleUrl, formulario);
        var concurrente = "Concurrente OIDC " + Guid.NewGuid().ToString("N")[..6];
        var respuestas = await Task.WhenAll(_cliente.PostAsync(ruta, new FormUrlEncodedContent(Campos(formulario, concurrente))),
            _cliente.PostAsync(ruta, new FormUrlEncodedContent(Campos(formulario, concurrente.ToUpperInvariant()))));
        try
        {
            Exigir(respuestas.Count(r => r.StatusCode == HttpStatusCode.Redirect) == 1
                && respuestas.Count(r => r.StatusCode == HttpStatusCode.OK) == 1, "OIDC-concurrencia-un-solo-exito");
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
        var nodoNombre = "Nodo aceptación OIDC " + Guid.NewGuid().ToString("N")[..6];
        var rutaNodo = $"/parques-solares/{parqueId:D}/nodos/nuevo";
        var formNodo = await ObtenerAsync(rutaNodo);
        var serie = Guid.NewGuid().ToString("N");
        using (var nuevo = await _cliente.PostAsync(rutaNodo, new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["_handler"]="alta-nodo", ["__RequestVerificationToken"]=Token(formNodo), ["Modelo.Nombre"]=nodoNombre,
            ["Modelo.Latitud"]="0", ["Modelo.Longitud"]="0", ["Modelo.NombreInversor"]="Inversor aceptación",
            ["Modelo.NumeroSerieInversor"]="I-"+serie, ["Modelo.NombreSensor"]="Sensor aceptación", ["Modelo.NumeroSerieSensor"]="S-"+serie
        })))
            Exigir(nuevo.IsSuccessStatusCode, "OIDC-regresion-alta-nodo");
        var nodoVacio = await db.Nodos.AsNoTracking().SingleAsync(n => n.Nombre == nodoNombre);
        var vacio = WebUtility.HtmlDecode(await ObtenerAsync($"/nodos/{nodoVacio.ID:D}"));
        Exigir(vacio.Contains("Este nodo todavía no tiene paneles solares"), "OIDC-lista-vacia");
        var parque = WebUtility.HtmlDecode(await ObtenerAsync($"/parques-solares/{parqueId:D}"));
        var listado = WebUtility.HtmlDecode(await ObtenerAsync("/parques-solares"));
        Exigir(parque.Contains(nodoNombre) && parque.Contains("Cantidad de paneles") && listado.Contains("Parque req006 A"), "OIDC-regresion-parques-y-equipos");
        var esperados = await new ParqueSolarRepository(db).ObtenerPorIdAsync(parqueId);
        Exigir(parque.Contains(esperados!.CapacidadTotalEstimadaMw.ToString("0.############################",CultureInfo.CurrentCulture)), "OIDC-capacidad-en-vista");
        await File.WriteAllTextAsync(Carpeta + "/oidc-rutas.json",JsonSerializer.Serialize(new { NodoVacio=nodoVacio.ID, Panel=panel.ID, Nombre=nombre }));
    }

    private async Task FallosAsync(string ruta, string detalleUrl, string formulario)
    {
        await using var sql = new MySqlConnection(_conexion); await sql.OpenAsync();
        await using var comando = sql.CreateCommand();
        comando.CommandText = "RENAME TABLE Paneles TO Paneles_req006_fallo";
        await comando.ExecuteNonQueryAsync();
        try
        {
            var carga = WebUtility.HtmlDecode(await ObtenerAsync(ruta));
            var detalle = WebUtility.HtmlDecode(await ObtenerAsync(detalleUrl));
            Exigir(carga.Contains("No se pudo cargar la información") && carga.Contains("Reintentar") && !carga.Contains("Nodo de destino:"), "OIDC-fallo-carga-con-reintento-sin-datos");
            Exigir(detalle.Contains("No se pudo cargar la información") && !detalle.Contains("Este nodo todavía no tiene paneles"), "OIDC-fallo-no-simula-lista-vacia");
        }
        finally { comando.CommandText = "RENAME TABLE Paneles_req006_fallo TO Paneles"; await comando.ExecuteNonQueryAsync(); }
        comando.CommandText = "CREATE TRIGGER req006_fallo_ace BEFORE INSERT ON Paneles FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='req006_controlado'";
        await comando.ExecuteNonQueryAsync();
        try
        {
            using var respuesta = await _cliente.PostAsync(ruta,new FormUrlEncodedContent(Campos(formulario,"Fallo aceptación")));
            var html=WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            Exigir(respuesta.StatusCode==HttpStatusCode.OK && html.Contains("No se pudo registrar el panel solar") && html.Contains("value=\"Fallo aceptación\"") && !html.Contains("Panel solar registrado"), "OIDC-fallo-guardado-conserva-entradas");
        }
        finally { comando.CommandText="DROP TRIGGER req006_fallo_ace"; await comando.ExecuteNonQueryAsync(); }
        using var recuperacion=await _cliente.PostAsync(ruta,new FormUrlEncodedContent(Campos(await ObtenerAsync(ruta),"Recuperación OIDC " + Guid.NewGuid().ToString("N")[..6])));
        Exigir(recuperacion.StatusCode==HttpStatusCode.Redirect,"OIDC-recuperacion-fallos-reales");
    }

    private async Task SinRolAsync(Guid nodoId, Guid parqueId)
    {
        var listado = await ObtenerAsync("/parques-solares");
        var parque = await ObtenerAsync($"/parques-solares/{parqueId:D}");
        Exigir(!WebUtility.HtmlDecode(parque).Contains("+ Agregar nodo") && !parque.Contains("enlace-nodo"), "OIDC-otro-rol-conserva-consultas-sin-acciones-admin");
        await using var db = Contexto(); var antes = await db.Paneles.CountAsync();
        var token = Token(listado);
        foreach (var ruta in new[] { $"/nodos/{nodoId:D}", $"/nodos/{nodoId:D}/paneles/nuevo" })
        {
            foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Post })
            {
                using var peticion = new HttpRequestMessage(metodo,ruta);
                if (metodo==HttpMethod.Post) peticion.Content=new FormUrlEncodedContent(new Dictionary<string,string>
                { ["_handler"]="alta-panel",["__RequestVerificationToken"]=token,["Formulario.Nombre"]="Sin permiso",["Formulario.Modelo"]="M",["Formulario.PotenciaNominalW"]="1" });
                using var respuesta=await _cliente.SendAsync(peticion);
                Exigir(respuesta.StatusCode==HttpStatusCode.Redirect && respuesta.Headers.Location!.ToString().Contains("acceso-denegado"),"OIDC-otro-rol-get-post-denegado");
                Exigir(!(await respuesta.Content.ReadAsStringAsync()).Contains("Nodo de destino"),"OIDC-denegado-sin-datos");
            }
        }
        Exigir(await db.Paneles.CountAsync()==antes,"OIDC-otro-rol-sin-escrituras");
    }

    public void Dispose() => _cliente.Dispose();
}
internal sealed class CriterioFallidoException(string criterio) : Exception(criterio);
