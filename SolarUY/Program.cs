using SolarUY.Components;
using SolarUY.Application.Monitoreo;
using SolarUY.Presentation.Monitoreo;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.Paneles;
using SolarUY.Application.Paneles.Servicios;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using System.Threading.RateLimiting;
using SolarUY.Infrastructure.Balanceo;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.IO.Pipelines;
using System.Text.Json.Serialization;
using SolarUY.Application.Asistente;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Presentation.Asistente;

var builder = WebApplication.CreateBuilder(args);
PersistenciaConfiguration.CargarConfiguracion(builder.Configuration,
    builder.Environment.ContentRootPath, builder.Environment.EnvironmentName, args);
AsistenteConfiguration.AgregarConfiguracion(builder);
builder.Services.ConfigureHttpJsonOptions(opciones => opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IConsultaOperativaRepository, ConsultaOperativaRepository>();
builder.Services.AddScoped<IConsultaOperativaService, ConsultaOperativaService>();
builder.Services.AddScoped<PeriodoConsultaService>();
builder.Services.AddScoped<IAsistenteService, AsistenteService>();
builder.Services.AddScoped<IClienteHerramientasMcp>(servicios => new ClienteHerramientasMcp((instante, cancelacion) =>
{
    var haciaServidor = new Pipe();
    var haciaCliente = new Pipe();
    IAsyncDisposable servidor = new ServidorAsistenteMcp(haciaServidor.Reader.AsStream(), haciaCliente.Writer.AsStream(),
        servicios.GetRequiredService<IConsultaOperativaService>(), instante, cancelacion);
    return Task.FromResult((haciaCliente.Reader.AsStream(), haciaServidor.Writer.AsStream(), servidor));
}));
var balanceo = BalanceoConfiguration.AgregarBalanceo(builder.Services, builder.Configuration);
// Los errores del conector pueden incluir SQL o datos del usuario en sus mensajes.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.None);
// Los mensajes internos del protocolo pueden contener respuestas y tokens del proveedor.
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.OpenIdConnect", LogLevel.None);
builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.None);
builder.Logging.AddFilter("System.IdentityModel", LogLevel.None);
// Evita registrar query strings de callbacks incluso al aumentar el nivel global.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NotificacionMedicion>();
builder.Services.AddSingleton<INotificacionMedicion>(servicios => servicios.GetRequiredService<NotificacionMedicion>());
builder.Services.AddSingleton<IPanelesMonitoreados>(servicios => servicios.GetRequiredService<NotificacionMedicion>());
if (balanceo.Habilitado)
    builder.Services.AddHostedService<SincronizacionMedicionesService>();

var conexion = PersistenciaConfiguration.ObtenerConexion(builder.Configuration);
builder.Services.AddDbContextFactory<SolarUyDbContext>(options =>
    PersistenciaConfiguration.ConfigurarContexto(options, conexion));
builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
builder.Services.AddScoped<INodoRepository, NodoRepository>();
builder.Services.AddScoped<INodoService, NodoService>();
builder.Services.AddScoped<IPanelRepository, PanelRepository>();
builder.Services.AddScoped<IPanelService, PanelService>();
builder.Services.AddScoped<IMedicionRepository, MedicionRepository>();
builder.Services.AddScoped<MonitoreoService>();
builder.Services.AddSingleton(TimeProvider.System);
SeguridadConfiguration.AgregarAutenticacionExterna(builder.Services, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        // Una respuesta con cuerpo evita reejecutar el POST en la página general de errores.
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Se alcanzó el límite de intentos. Esperá un minuto para volver a iniciar sesión.", cancellationToken);
    };
    options.AddPolicy("inicio-sesion", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

var app = builder.Build();
app.Use(async (contexto, siguiente) =>
{
    contexto.Response.OnStarting(() =>
    {
        contexto.Response.Headers["X-SolarUY-Instancia"] = balanceo.Instancia;
        return Task.CompletedTask;
    });
    await siguiente(contexto);
});
BalanceoConfiguration.UsarOrigenPublico(app, balanceo);
// El sondeo interno no necesita redirección HTTPS, cookies ni autorización.
app.Map("/health/ready", salud => salud.UseHealthChecks("", new HealthCheckOptions
{
    ResponseWriter = async (contexto, resultado) =>
    {
        contexto.Response.ContentType = "text/plain";
        await contexto.Response.WriteAsync(contexto.Response.StatusCode == 200 ? "Disponible" : "No disponible");
    }
}));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Los hubs deben conservar sus estados de protocolo y no reejecutarse como formularios Razor.
app.UseWhen(contexto => !contexto.Request.Path.StartsWithSegments("/hubs") &&
    !contexto.Request.Path.StartsWithSegments("/_blazor") && !contexto.Request.Path.StartsWithSegments("/api"), paginas =>
    paginas.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapPost("/cuenta/autenticar", async (HttpContext context,
    IAntiforgery antiforgery, ILogger<Program> logger) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("El formulario venció. Volvé a iniciar sesión.");
    }

    var propiedades = new AuthenticationProperties
    {
        RedirectUri = "/parques-solares",
        IsPersistent = false
    };
    try
    {
        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, propiedades);
        return Results.Empty;
    }
    catch (Exception)
    {
        logger.LogWarning("No se pudo iniciar el acceso externo. Código: oidc_challenge.");
        return Results.LocalRedirect("/cuenta/iniciar-sesion?error=proveedor");
    }
}).RequireRateLimiting("inicio-sesion");

app.MapPost("/cuenta/cerrar-sesion", async (HttpContext context, IAntiforgery antiforgery,
    ILogger<Program> logger) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("El formulario venció. Recargá la página.");
    }

    var sesion = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    SeguridadConfiguration.EliminarCookieAnterior(context);
    var propiedades = new AuthenticationProperties { RedirectUri = "/" };
    var token = sesion.Properties?.GetTokenValue("id_token");
    if (!string.IsNullOrEmpty(token))
        propiedades.SetParameter("id_token_hint", token);
    try
    {
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, propiedades);
        return Results.Empty;
    }
    catch (Exception)
    {
        logger.LogWarning("Sesión local cerrada; no se completó el cierre externo. Código: oidc_logout.");
        return Results.LocalRedirect("/cuenta/iniciar-sesion?error=cierre");
    }
}).RequireAuthorization();

app.MapHub<MonitoreoHub>("/hubs/monitoreo");
app.MapearAsistente();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);

app.Run();
