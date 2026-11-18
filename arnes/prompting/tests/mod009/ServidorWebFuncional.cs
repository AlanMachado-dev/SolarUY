using System.IO.Pipelines;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using SolarUY.Presentation.Asistente;

static class ServidorWebFuncional
{
    public static async Task EjecutarAsync(string conexion, Escenario escenario)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ServidorWebFuncional).Assembly.GetName().Name,
            ContentRootPath = Directory.GetCurrentDirectory(), WebRootPath = Path.GetFullPath("SolarUY/wwwroot")
        });
        builder.WebHost.UseUrls("http://127.0.0.1:43339").UseStaticWebAssets();
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization(); builder.Services.AddHttpContextAccessor();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddScoped<SesionCookieEventsHandler>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opciones =>
        {
            opciones.Cookie.Name = "Mod009.Sintetica";
            opciones.SlidingExpiration = false;
            opciones.EventsType = typeof(SesionCookieEventsHandler);
        });
        builder.Services.AddSingleton<TimeProvider>(new RelojFuncional(escenario.Instante));
        builder.Services.AddSingleton(new AsistenteOptions { Habilitado = true, ClaveApi = "auxiliar-sin-credencial" });
        builder.Services.AddSingleton(escenario);
        builder.Services.AddDbContextFactory<SolarUyDbContext>(opciones => PersistenciaConfiguration.ConfigurarContexto(opciones, conexion));
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddScoped<IConsultaOperativaRepository, ConsultaOperativaRepository>();
        builder.Services.AddScoped<IConsultaOperativaService, ConsultaOperativaService>();
        builder.Services.AddScoped<PeriodoConsultaService>();
        builder.Services.AddScoped<FormatoRespuestaAsistenteService>();
        builder.Services.AddScoped<IAsistenteService, AsistenteService>();
        builder.Services.AddScoped<IModeloConversacional, ModeloWebFuncional>();
        builder.Services.AddScoped<IClienteHerramientasMcp>(servicios => new ClienteHerramientasMcp((instante, cancelacion) =>
        {
            var entrada = new Pipe(); var salida = new Pipe();
            IAsyncDisposable servidor = new ServidorAsistenteMcp(entrada.Reader.AsStream(), salida.Writer.AsStream(),
                servicios.GetRequiredService<IConsultaOperativaService>(), instante, cancelacion);
            return Task.FromResult((salida.Reader.AsStream(), entrada.Writer.AsStream(), servidor));
        }));
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        app.UseStaticFiles(); app.MapStaticAssets();
        app.MapGet("/entrar/{rol}", async (string rol, HttpContext contexto) =>
        {
            var identidad = new ClaimsIdentity([new("sub", "mod009-sintetico"),
                new(ClaimTypes.Role, rol == "Expira" ? "Operador" : rol)], CookieAuthenticationDefaults.AuthenticationScheme);
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad),
                new AuthenticationProperties { IssuedUtc = escenario.Instante.AddHours(-1),
                    ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(rol == "Expira" ? 15 : 600) });
            return Results.Redirect("/");
        });
        app.MapGet("/salir", async (HttpContext contexto) =>
        {
            await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });
        app.MapearAsistente();
        app.MapRazorComponents<SolarUY.Mod009.AppPrueba>().AddInteractiveServerRenderMode();
        Console.WriteLine("Web aislada: http://127.0.0.1:43339. Sesión y modelo sintéticos; sin llamadas externas.");
        await app.RunAsync();
    }
}

sealed class ModeloWebFuncional(Escenario escenario) : IModeloConversacional
{
    public Task<PasoModeloDto> CompletarAsync(TurnoModeloDto turno, CancellationToken cancellationToken)
    {
        if (turno.Intercambios.Count == 0)
            return Task.FromResult(new PasoModeloDto(CodigoAsistente.Exito, null,
                [new("resumen", "ResumirMediciones", JsonSerializer.Serialize(new { consulta = new ConsultaOperativaDto
                    { Tipo = "Panel", Id = escenario.Principal, Periodo = "Ultimos", CantidadPeriodo = 2, UnidadPeriodo = "Minutos" } }))]));
        using var documento = JsonDocument.Parse(turno.Intercambios.Last().Resultados[0].ContenidoJson);
        var presentacion = documento.RootElement.GetProperty("Presentacion");
        var promedio = presentacion.GetProperty("Valores").EnumerateArray()
            .Single(valor => valor.GetProperty("Campo").GetString() == "Datos[0].Potencia.PromedioObservadoW").GetProperty("Texto").GetString();
        return Task.FromResult(new PasoModeloDto(CodigoAsistente.Exito,
            $"**Promedio disponible:** {promedio}.\n\n{presentacion.GetProperty("Periodo").GetString()}\n\\- Mediciones simuladas.\n<script>window.mod009Inyeccion=true</script>", []));
    }
}
