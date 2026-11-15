using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Energia;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Components;
using SolarUY.Domain.Paneles;
using SolarUY.Infrastructure.Balanceo;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using SolarUY.Presentation.Monitoreo;

internal static class ServidorPrueba
{
    public static async Task EjecutarAsync(string conexion, int puerto, bool oidc = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetFullPath("SolarUY"),
            ApplicationName = typeof(ServidorPrueba).Assembly.GetName().Name
        });
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        using var certificado = oidc ? AceptacionOidc.CargarCertificado(builder, puerto) : null;
        if (!oidc)
            builder.WebHost.UseUrls($"http://127.0.0.1:{puerto}");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddSignalR();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<SesionCookieEventsHandler>();
        if (oidc)
        {
            SeguridadConfiguration.AgregarAutenticacionExterna(builder.Services, builder.Configuration);
        }
        else
        {
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opciones =>
            {
                opciones.Cookie.Name = $"Req012.{puerto}";
                opciones.EventsType = typeof(SesionCookieEventsHandler);
            });
        }
        builder.Services.AddDbContextFactory<SolarUyDbContext>(opciones => PersistenciaConfiguration.ConfigurarContexto(opciones, conexion));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(new BalanceoOptions { Habilitado = true, IntervaloSincronizacionSegundos = 0.25 });
        builder.Services.AddSingleton<NotificacionMedicion>();
        builder.Services.AddSingleton<INotificacionMedicion>(s => s.GetRequiredService<NotificacionMedicion>());
        builder.Services.AddSingleton<IPanelesMonitoreados>(s => s.GetRequiredService<NotificacionMedicion>());
        builder.Services.AddScoped<IControlPanelRepository, ControlPanelRepository>();
        builder.Services.AddScoped<IMedicionRepository, MedicionRepository>();
        builder.Services.AddScoped<INodoRepository, NodoRepository>();
        builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddScoped<IEnergiaRepository, EnergiaRepository>();
        builder.Services.AddScoped<MonitoreoService>();
        builder.Services.AddScoped<ControlPanelService>();
        builder.Services.AddScoped<EnergiaService>();
        builder.Services.AddScoped<VencimientosMantenimientoService>();
        builder.Services.AddHostedService<ProcesamientoMantenimientoService>();
        builder.Services.AddHostedService<SincronizacionMedicionesService>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapHub<MonitoreoHub>("/hubs/monitoreo");
        app.MapearControlSensor();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);

        if (oidc)
        {
            AceptacionOidc.Mapear(app);
            await app.RunAsync();
            return;
        }

        // Estas rutas solo existen en el arnés local con datos e identidades de prueba.
        app.MapGet("/req012/entrar/{rol}", async (string rol, HttpContext contexto) =>
        {
            if (rol is not ("Administrador" or "Operador"))
                return Results.NotFound();
            var usuario = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, rol)], CookieAuthenticationDefaults.AuthenticationScheme));
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, usuario);
            return Results.Ok();
        });
        app.MapGet("/req012/panel", async (SolarUyDbContext db) => await db.Paneles.AsNoTracking()
            .Where(panel => panel.Nombre.Valor == "Panel navegador").Select(panel => new { panel.ID, NodoId = panel.Nodo.ID }).SingleAsync());
        app.MapGet("/req012/panel-reinicio", async (SolarUyDbContext db) => await db.Paneles.AsNoTracking()
            .Where(panel => panel.Nombre.Valor == "Panel reinicio").Select(panel => panel.ID).SingleAsync());
        app.MapGet("/req012/mediciones/{panelId:guid}", async (Guid panelId, SolarUyDbContext db) =>
            await db.Set<Medicion>().CountAsync(medicion => EF.Property<Guid>(medicion, "IdPanel") == panelId));
        app.MapGet("/req012/mantenimiento/{panelId:guid}", async (Guid panelId, SolarUyDbContext db) =>
            await db.Set<Mantenimiento>().Where(registro => EF.Property<Guid>(registro, "IdPanel") == panelId)
                .OrderByDescending(registro => registro.FechaProgramada).Select(registro =>
                    new { registro.ID, registro.InicioProcesadoUtc, registro.FechaRealizada, registro.Observaciones }).FirstOrDefaultAsync());
        app.MapPost("/req012/escritura/{bloquear:bool}", async (bool bloquear, SolarUyDbContext db) =>
        {
            if (bloquear)
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER req012_web_fallo BEFORE INSERT ON Medicion FOR EACH ROW " +
                    "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req012'");
            else
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER req012_web_fallo");
            return Results.NoContent();
        });
        await app.RunAsync();
    }
}
