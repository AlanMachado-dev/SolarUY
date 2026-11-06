global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using SolarUY.Presentation.Asistente;

static class ServidorFuncional
{
    public static async Task EjecutarAsync(string conexion, bool certificadoAceptacion = false)
    {
        using var clave = RSA.Create(2048);
        var solicitud = new CertificateRequest("CN=localhost", clave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var certificadoFuente = certificadoAceptacion
            ? X509Certificate2.CreateFromPemFile(".solaruy-private/localhost.crt", ".solaruy-private/localhost.key")
            : certificado;
        using var certificadoTls = X509CertificateLoader.LoadPkcs12(certificadoFuente.Export(X509ContentType.Pfx), null);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ServidorFuncional).Assembly.GetName().Name,
            ContentRootPath = Directory.GetCurrentDirectory(),
            WebRootPath = Path.GetFullPath("SolarUY/wwwroot")
        });
        builder.WebHost.ConfigureKestrel(servidor => servidor.ListenLocalhost(43320, escucha => escucha.UseHttps(certificadoTls)));
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<SesionCookieEventsHandler>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opciones =>
        {
            opciones.Cookie.Name = "Req009.Aislada";
            opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            opciones.EventsType = typeof(SesionCookieEventsHandler);
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(new AsistenteOptions { Habilitado = true, ClaveApi = "doble-funcional-no-credencial" });
        builder.Services.ConfigureHttpJsonOptions(opciones => opciones.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        builder.Services.AddDbContextFactory<SolarUyDbContext>(opciones => PersistenciaConfiguration.ConfigurarContexto(opciones, conexion));
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddScoped<IConsultaOperativaRepository, ConsultaOperativaRepository>();
        builder.Services.AddScoped<IConsultaOperativaService, ConsultaOperativaService>();
        builder.Services.AddScoped<PeriodoConsultaService>();
        builder.Services.AddScoped<IAsistenteService, AsistenteService>();
        builder.Services.AddScoped<IModeloConversacional, ModeloFuncional>();
        builder.Services.AddSingleton<InyeccionesFuncionales>();
        builder.Services.AddScoped<InterrupcionMcpFuncional>();
        builder.Services.AddScoped<IClienteHerramientasMcp>(servicios => new ClienteHerramientasMcp((instante, cancelacion) =>
        {
            var a = new Pipe(); var b = new Pipe();
            servicios.GetRequiredService<InterrupcionMcpFuncional>().Interrumpir = () => { a.Writer.Complete(); b.Writer.Complete(); };
            IAsyncDisposable servidor = new ServidorAsistenteMcp(a.Reader.AsStream(), b.Writer.AsStream(),
                servicios.GetRequiredService<IConsultaOperativaService>(), instante, cancelacion);
            return Task.FromResult((b.Reader.AsStream(), a.Writer.AsStream(), servidor));
        }));
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        app.UseStaticFiles();
        app.MapStaticAssets();
        app.MapGet("/entrar/{rol}", async (string rol, HttpContext contexto) =>
        {
            // Solo en el arnés HTTPS loopback: identidad sintética, no prueba OIDC.
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new("sub", "req009-sintetico"),
                new(ClaimTypes.Role, rol == "Expira" ? "Operador" : rol)], CookieAuthenticationDefaults.AuthenticationScheme));
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = rol == "Expira" ? DateTimeOffset.UtcNow.AddSeconds(15) : DateTimeOffset.UtcNow.AddMinutes(10) });
            return Results.Redirect("/");
        });
        app.MapGet("/salir", async (HttpContext contexto) =>
        {
            await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });
        app.MapearAsistente();
        app.MapRazorComponents<SolarUY.Req009.VerificacionApp>().AddInteractiveServerRenderMode();
        Console.WriteLine("Arnés aislado HTTPS 43320: MySQL req009, MCP real, identidad y modelo sintéticos. Sin aceptación externa.");
        await app.RunAsync();
    }
}

sealed class InyeccionesFuncionales
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _casos = new();
    public bool PrimeraVez(string caso) => _casos.TryAdd(caso, 0);
}

sealed class InterrupcionMcpFuncional
{
    public Action? Interrumpir { get; set; }
}

sealed class ModeloFuncional(InyeccionesFuncionales inyecciones, InterrupcionMcpFuncional interrupcion) : IModeloConversacional
{
    public async Task<PasoModeloDto> CompletarAsync(TurnoModeloDto turno, CancellationToken cancellationToken)
    {
        var pregunta = turno.Mensajes.Last().Texto;
        if (turno.Intercambios.Count == 0 && pregunta.StartsWith("probar-cuota") && inyecciones.PrimeraVez(pregunta))
        {
            var modelo = new GeminiConversacional(new HttpClient(new RespuestaHttp(System.Net.HttpStatusCode.TooManyRequests)),
                new AsistenteOptions { Habilitado = true, ClaveApi = "doble-funcional-no-credencial" },
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiConversacional>.Instance);
            return await modelo.CompletarAsync(turno, cancellationToken);
        }
        if (turno.Intercambios.Count == 0 && pregunta.StartsWith("probar-mcp") && inyecciones.PrimeraVez(pregunta))
            interrupcion.Interrumpir?.Invoke();
        if (turno.Mensajes.Last().Texto.Contains("demora")) await Task.Delay(2500, cancellationToken);
        if (turno.Intercambios.Count == 0)
            return new(CodigoAsistente.Exito, null, [new("funcional", "ResumirMediciones", "{\"consulta\":{\"Tipo\":\"Global\",\"Agrupacion\":\"Global\"}}")]);
        return new(CodigoAsistente.Exito, "Respuesta funcional simulada; consulta MCP real. <script>window.inyeccion=true</script>", []);
    }
}
