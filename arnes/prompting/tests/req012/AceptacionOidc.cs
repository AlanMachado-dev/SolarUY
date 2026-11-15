using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Monitoreo;
using SolarUY.Infrastructure.Persistencia;

internal static class AceptacionOidc
{
    public static X509Certificate2 CargarCertificado(WebApplicationBuilder builder, int puerto)
    {
        var raiz = Directory.GetCurrentDirectory();
        builder.Configuration.AddJsonFile(Path.Combine(raiz, ".solaruy-acceptance/req009/appsettings.Local.json"),
            optional: false, reloadOnChange: false);
        using var pem = X509Certificate2.CreateFromPemFile(Path.Combine(raiz, ".solaruy-private/localhost.crt"),
            Path.Combine(raiz, ".solaruy-private/localhost.key"));
        var certificado = X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(puerto, opciones => opciones.UseHttps(certificado)));
        return certificado;
    }

    public static void Mapear(WebApplication app)
    {
        app.MapPost("/cuenta/autenticar", async (HttpContext contexto, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest();
            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/req012/aceptacion" },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        });
        app.MapPost("/cuenta/cerrar-sesion", async (HttpContext contexto, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest();

            var sesion = await contexto.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var propiedades = new AuthenticationProperties { RedirectUri = "/" };
            var token = sesion.Properties?.GetTokenValue("id_token");
            if (!string.IsNullOrEmpty(token))
                propiedades.SetParameter("id_token_hint", token);
            return Results.SignOut(propiedades, [OpenIdConnectDefaults.AuthenticationScheme]);
        }).RequireAuthorization();
        app.MapGet("/req012/aceptacion", MostrarAsync);
        app.MapPost("/req012/aceptacion", VerificarRechazosAsync);
    }

    private static async Task<IResult> MostrarAsync(HttpContext contexto, IAntiforgery antiforgery, SolarUyDbContext db)
    {
        var panelId = await ObtenerPanelAsync(db, contexto.RequestAborted);
        var token = antiforgery.GetAndStoreTokens(contexto);
        var rol = contexto.User.Identity?.IsAuthenticated != true ? "Sin sesión" :
            contexto.User.IsInRole("Administrador") ? "Administrador" :
            contexto.User.IsInRole("Operador") ? "Operador" : "Sin rol reconocido";
        var codificador = HtmlEncoder.Default;
        return Results.Content($"""
            <!doctype html><html lang="es"><meta charset="utf-8"><title>Aceptación req012</title>
            <h1>Aceptación req012</h1><p>Rol confirmado por el servidor: {rol}</p>
            <p><a href="/paneles/{panelId:D}/monitoreo">Abrir monitoreo del panel de prueba</a></p>
            <p><a href="/cuenta/iniciar-sesion">Iniciar sesión</a></p>
            <form method="post">
                <input type="hidden" name="{codificador.Encode(token.FormFieldName)}" value="{codificador.Encode(token.RequestToken!)}">
                <button type="submit">Comprobar rechazo de órdenes</button>
            </form>
            </html>
            """, "text/html; charset=utf-8");
    }

    private static async Task<IResult> VerificarRechazosAsync(HttpContext contexto, IAntiforgery antiforgery,
        SolarUyDbContext db, ControlPanelService control, MonitoreoService monitoreo, TimeProvider reloj)
    {
        if (!await antiforgery.IsRequestValidAsync(contexto))
            return Results.BadRequest();
        // Se envían al caso de uso las órdenes que este rol tiene prohibidas.
        var autenticado = contexto.User.Identity?.IsAuthenticated == true;
        if (contexto.User.IsInRole("Administrador"))
            return Results.BadRequest("Usá una sesión de Operador o una ventana sin sesión.");
        var cancelacion = contexto.RequestAborted;
        var panelId = await ObtenerPanelAsync(db, cancelacion);
        var antes = await monitoreo.ConsultarAsync(panelId, cancelacion);
        OrdenControlPanel[] restringidas = [new SimularFallaPanel(), new SimularArregloPanel(),
            new DesactivarPanel(), new ActivarPanel()];
        if (!autenticado)
        {
            restringidas = [.. restringidas,
                new ProgramarMantenimientoDto("Verificación", reloj.GetUtcNow().UtcDateTime, "Aceptación req012"),
                new FinalizarMantenimientoDto(antes?.Mantenimiento?.Id ?? Guid.Empty, null)];
        }

        var resultados = new Dictionary<string, string>();
        foreach (var orden in restringidas)
        {
            var resultado = await control.EjecutarAsync(contexto.User, panelId, orden, cancelacion);
            resultados.Add(orden.GetType().Name, resultado.Codigo.ToString());
        }
        var despues = await monitoreo.ConsultarAsync(panelId, cancelacion);
        return Results.Json(new
        {
            Rol = autenticado && contexto.User.IsInRole("Operador") ? "Operador" : "Sin sesión",
            Ordenes = resultados,
            ControlSinCambios = antes?.RevisionControl == despues?.RevisionControl && antes?.EstadoControl == despues?.EstadoControl
        });
    }

    private static Task<Guid> ObtenerPanelAsync(SolarUyDbContext db, CancellationToken cancelacion) =>
        db.Paneles.AsNoTracking().Where(panel => panel.Nombre.Valor == "Panel navegador").Select(panel => panel.ID)
            .SingleAsync(cancelacion);
}
