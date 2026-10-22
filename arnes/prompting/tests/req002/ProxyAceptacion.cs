// Arnés funcional HTTPS. No es parte de SolarUY ni agrega endpoints a la aplicación.
// Las cookies y el callback consumido se conservan exclusivamente en memoria.
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

var raiz = Environment.GetEnvironmentVariable("SOLARUY_ACEPTACION_RAIZ")
    ?? throw new InvalidOperationException("Falta la raíz del arnés.");
var salida = Path.Combine(raiz, "artifacts", "req003");
using var almacen = new X509Store(StoreName.My, StoreLocation.CurrentUser);
almacen.Open(OpenFlags.ReadOnly);
var certificado = almacen.Certificates.Cast<X509Certificate2>()
    .Where(cert => cert.HasPrivateKey && cert.NotAfter > DateTime.Now && cert.NotBefore < DateTime.Now
        && cert.Extensions.Cast<X509Extension>().Any(extension => extension.Oid?.Value == "1.3.6.1.4.1.311.84.1.1"))
    .OrderByDescending(cert => cert.NotAfter).First();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.None);
builder.Services.AddDataProtection().SetApplicationName("SolarUY");
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(7178, listener => listener.UseHttps(certificado)));
var app = builder.Build();
var memoria = new MemoriaAceptacion(salida, app.Services.GetRequiredService<IDataProtectionProvider>());
using var cliente = new HttpClient(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(75) };
app.Run(async contexto =>
{
    try
    {
        using var cuerpo = new MemoryStream();
        await contexto.Request.Body.CopyToAsync(cuerpo);
        var headers = contexto.Request.Headers.ToDictionary(par => par.Key, par => par.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var peticion = new Peticion(contexto.Request.Method, contexto.Request.Path + contexto.Request.QueryString,
            cuerpo.ToArray(), headers);
        memoria.CapturarCookie(contexto.Request.Headers.Cookie.ToString());
        var respuesta = await memoria.EnviarAsync(cliente, peticion);
        memoria.CapturarRespuesta(peticion, respuesta);
        contexto.Response.StatusCode = respuesta.Estado;
        foreach (var par in respuesta.Headers.Where(par => !MemoriaAceptacion.Salto.Contains(par.Key)))
            contexto.Response.Headers[par.Key] = par.Value;
        contexto.Response.ContentLength = respuesta.Cuerpo.Length;
        await contexto.Response.Body.WriteAsync(respuesta.Cuerpo);
    }
    catch (Exception)
    {
        contexto.Response.StatusCode = 502;
        await contexto.Response.WriteAsync("El servidor de aceptación no está disponible.");
    }
});
_ = memoria.ControlarAsync(cliente, app.Lifetime.ApplicationStopping);
await app.RunAsync();

internal sealed record Peticion(string Metodo, string Ruta, byte[] Cuerpo, Dictionary<string, string> Headers);
internal sealed record Respuesta(int Estado, Dictionary<string, string[]> Headers, byte[] Cuerpo);

internal sealed class MemoriaAceptacion
{
    internal static readonly HashSet<string> Salto = new(StringComparer.OrdinalIgnoreCase)
        { "Connection", "Transfer-Encoding", "Keep-Alive", "TE", "Trailer", "Upgrade", "Content-Length", "Host" };
    private const string NombreCookie = "SolarUY.Sesion.Oidc";
    private readonly string _salida;
    private readonly TicketDataFormat _formato;
    private readonly object _candado = new();
    private string _cookie = "";
    private string _inicial = "";
    private DateTimeOffset? _venceInicial;
    private Peticion? _callback;
    private int _callbacks;

    internal MemoriaAceptacion(string salida, IDataProtectionProvider provider)
    {
        _salida = salida;
        _formato = new TicketDataFormat(provider.CreateProtector(
            "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", "Cookies", "v2"));
    }

    internal async Task<Respuesta> EnviarAsync(HttpClient cliente, Peticion peticion)
    {
        using var solicitud = new HttpRequestMessage(new HttpMethod(peticion.Metodo), "https://localhost:7443" + peticion.Ruta);
        if (peticion.Cuerpo.Length > 0 || peticion.Metodo == "POST")
            solicitud.Content = new ByteArrayContent(peticion.Cuerpo);
        foreach (var par in peticion.Headers.Where(par => !Salto.Contains(par.Key)))
            if (!solicitud.Headers.TryAddWithoutValidation(par.Key, par.Value))
                solicitud.Content?.Headers.TryAddWithoutValidation(par.Key, par.Value);
        solicitud.Headers.Host = "localhost:7178";
        using var respuesta = await cliente.SendAsync(solicitud);
        var headers = respuesta.Headers.Concat(respuesta.Content.Headers)
            .ToDictionary(par => par.Key, par => par.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        return new Respuesta((int)respuesta.StatusCode, headers, await respuesta.Content.ReadAsByteArrayAsync());
    }

    private static IEnumerable<string> ValoresCookie(Respuesta respuesta) =>
        respuesta.Headers.TryGetValue("Set-Cookie", out var valores) ? valores : [];

    private static bool HayCookieNueva(Respuesta respuesta) => ValoresCookie(respuesta)
        .Any(valor => valor.StartsWith(NombreCookie, StringComparison.Ordinal)
            && valor.Split(';', 2)[0].Split('=', 2)[1].Length > 0);

    internal void CapturarCookie(string encabezado)
    {
        var partes = encabezado.Split(';').Select(parte => parte.Trim())
            .Where(parte => parte.StartsWith(NombreCookie, StringComparison.Ordinal)).ToArray();
        if (partes.Length == 0) return;
        lock (_candado)
        {
            _cookie = string.Join("; ", partes);
            if (_inicial.Length != 0) return;
            var valor = partes.SingleOrDefault(parte => parte.StartsWith(NombreCookie + "=", StringComparison.Ordinal))?
                .Split('=', 2)[1];
            if (valor is null || valor.StartsWith("chunks-", StringComparison.Ordinal)) return;
            var ticket = _formato.Unprotect(valor);
            if (ticket is null) return;
            _inicial = _cookie;
            _venceInicial = ticket.Properties.ExpiresUtc;
            File.WriteAllText(Path.Combine(_salida, "sesion-resumen.json"), JsonSerializer.Serialize(new
            {
                fechaUtc = DateTimeOffset.UtcNow, ticketValido = true,
                emitidaUtc = ticket.Properties.IssuedUtc, venceUtc = ticket.Properties.ExpiresUtc,
                duracionMinutos = (ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc)?.TotalMinutes,
                persistente = ticket.Properties.IsPersistent, permiteRenovar = ticket.Properties.AllowRefresh,
                tokens = ticket.Properties.GetTokens().Select(token => token.Name).ToArray(),
                soloIdToken = ticket.Properties.GetTokens().Select(token => token.Name).SequenceEqual(["id_token"]),
                administrador = ticket.Principal.IsInRole("Administrador"),
                bytesEncabezadoCookie = Encoding.UTF8.GetByteCount(_cookie)
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    internal void CapturarRespuesta(Peticion peticion, Respuesta respuesta)
    {
        if (peticion.Ruta.Split('?', 2)[0] == "/signin-oidc" && HayCookieNueva(respuesta))
            lock (_candado) { _callback = peticion; _callbacks++; }
    }

    private Task<Respuesta> ClienteAsync(HttpClient cliente, string metodo, string ruta, string cookie = "",
        Dictionary<string, string>? datos = null)
    {
        var headers = new Dictionary<string, string> { ["Accept-Encoding"] = "identity", ["Cookie"] = cookie };
        var cuerpo = Array.Empty<byte>();
        if (datos is not null)
        {
            headers["Content-Type"] = "application/x-www-form-urlencoded";
            cuerpo = Encoding.UTF8.GetBytes(string.Join('&', datos.Select(par =>
                Uri.EscapeDataString(par.Key) + "=" + Uri.EscapeDataString(par.Value))));
        }
        return EnviarAsync(cliente, new Peticion(metodo, ruta, cuerpo, headers));
    }

    private static bool Redirige(Respuesta respuesta, string destino) => respuesta.Estado == 302
        && respuesta.Headers.TryGetValue("Location", out var valores) && valores.Any(valor => valor.Contains(destino, StringComparison.Ordinal));

    private async Task<object> EjecutarAsync(HttpClient cliente, string accion)
    {
        string cookie, inicial; Peticion? callback; int callbacks; DateTimeOffset? vence;
        lock (_candado) { cookie = _cookie; inicial = _inicial; callback = _callback; callbacks = _callbacks; vence = _venceInicial; }
        if (accion == "estado") return new { sesionCapturada = cookie.Length > 0, callbackCapturado = callback is not null, callbacks, venceInicialUtc = vence };
        if (accion == "csrf-cierre")
        {
            var antes = await ClienteAsync(cliente, "GET", "/parques-solares/nuevo", cookie);
            var sin = await ClienteAsync(cliente, "POST", "/cuenta/cerrar-sesion", cookie, []);
            var invalido = await ClienteAsync(cliente, "POST", "/cuenta/cerrar-sesion", cookie, new() { ["__RequestVerificationToken"] = "invalido" });
            var despues = await ClienteAsync(cliente, "GET", "/parques-solares/nuevo", cookie);
            return new { autenticadoAntes = antes.Estado == 200, sinToken400 = sin.Estado == 400,
                tokenInvalido400 = invalido.Estado == 400, sesionConservada = despues.Estado == 200,
                sinCookieNueva = !HayCookieNueva(sin) && !HayCookieNueva(invalido) };
        }
        if (accion == "cookie-anterior")
        {
            var actual = await ClienteAsync(cliente, "GET", "/parques-solares/nuevo", cookie);
            var anterior = await ClienteAsync(cliente, "GET", "/parques-solares/nuevo", cookie.Replace(NombreCookie, "SolarUY.Sesion", StringComparison.Ordinal));
            return new { ticketRealNombreActualAceptado = actual.Estado == 200,
                mismoTicketNombreAnteriorRechazado = Redirige(anterior, "/cuenta/iniciar-sesion") };
        }
        if (accion == "callback-repetido")
        {
            if (callback is null) throw new InvalidOperationException();
            var headers = new Dictionary<string, string>(callback.Headers, StringComparer.OrdinalIgnoreCase);
            headers["Cookie"] = string.Join("; ", headers.GetValueOrDefault("Cookie", "").Split(';')
                .Where(parte => !parte.Trim().StartsWith("SolarUY.Sesion", StringComparison.Ordinal)));
            var sin = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            sin["Cookie"] = string.Join("; ", headers["Cookie"].Split(';').Where(parte => !parte.Contains(".Correlation.", StringComparison.Ordinal)));
            var sinCorrelacion = await EnviarAsync(cliente, callback with { Headers = sin });
            var esPost = callback.Metodo == "POST";
            var pares = QueryHelpers.ParseQuery(esPost ? Encoding.UTF8.GetString(callback.Cuerpo)
                : new Uri("https://localhost" + callback.Ruta).Query)
                .ToDictionary(par => par.Key, par => par.Value.ToString());
            pares["state"] = "alterado";
            var formulario = string.Join('&', pares.Select(par => Uri.EscapeDataString(par.Key) + "=" + Uri.EscapeDataString(par.Value)));
            var manipulado = await EnviarAsync(cliente, callback with { Headers = headers,
                Ruta = esPost ? callback.Ruta : "/signin-oidc?" + formulario,
                Cuerpo = esPost ? Encoding.UTF8.GetBytes(formulario) : callback.Cuerpo });
            var consumido = await EnviarAsync(cliente, callback with { Headers = headers });
            object Describir(Respuesta respuesta) => new { errorControlado = Redirige(respuesta, "/cuenta/iniciar-sesion?error=proveedor"), sinCookieNueva = !HayCookieNueva(respuesta) };
            return new { sinCorrelacion = Describir(sinCorrelacion), stateAlterado = Describir(manipulado), codigoConsumido = Describir(consumido) };
        }
        if (accion == "limite-inicio")
        {
            var estados = new List<int>();
            for (var indice = 0; indice < 11; indice++) estados.Add((await ClienteAsync(cliente, "POST", "/cuenta/autenticar", datos: [])).Estado);
            return new { estados, limite429 = estados.Contains(429), callbacksRealesAntes = callbacks };
        }
        if (accion == "comprobar-caducidad")
        {
            var respuesta = await ClienteAsync(cliente, "GET", "/parques-solares/nuevo", inicial);
            return new { venceInicialUtc = vence, fechaUtc = DateTimeOffset.UtcNow,
                vencida = vence is not null && DateTimeOffset.UtcNow >= vence,
                http = respuesta.Estado, requiereNuevoIngreso = Redirige(respuesta, "/cuenta/iniciar-sesion"), sinRenovacion = !HayCookieNueva(respuesta) };
        }
        if (accion == "denegacion-directa")
        {
            var get = await ClienteAsync(cliente, "GET", "/parques-solares/nuevo", cookie);
            var post = await ClienteAsync(cliente, "POST", "/parques-solares/nuevo", cookie, []);
            return new { getDenegado = get.Estado == 403 || Redirige(get, "/cuenta/acceso-denegado"),
                postDenegado = post.Estado == 403 || Redirige(post, "/cuenta/acceso-denegado") };
        }
        throw new InvalidOperationException();
    }

    internal async Task ControlarAsync(HttpClient cliente, CancellationToken cancellationToken)
    {
        string? ultimo = null;
        var ruta = Path.Combine(_salida, "proxy-control.json");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(ruta))
                {
                    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(ruta, cancellationToken));
                    var id = json.RootElement.GetProperty("id").GetString();
                    var accion = json.RootElement.GetProperty("accion").GetString()!;
                    if (id != ultimo && Guid.TryParseExact(id, "N", out _))
                    {
                        ultimo = id;
                        if (!new[] { "estado", "csrf-cierre", "cookie-anterior", "callback-repetido", "limite-inicio", "comprobar-caducidad", "denegacion-directa" }.Contains(accion)) continue;
                        object resultado;
                        try { resultado = new { id, accion, fechaUtc = DateTimeOffset.UtcNow, resultado = await EjecutarAsync(cliente, accion) }; }
                        catch (Exception error) { resultado = new { id, accion, error = error.GetType().Name }; }
                        await File.WriteAllTextAsync(Path.Combine(_salida, "proxy-" + accion + ".json"),
                            JsonSerializer.Serialize(resultado, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
                        Console.WriteLine("Control terminado: " + accion);
                    }
                }
                await Task.Delay(200, cancellationToken);
            }
            catch (Exception error) when (error is IOException or JsonException or OperationCanceledException) { }
        }
    }
}
