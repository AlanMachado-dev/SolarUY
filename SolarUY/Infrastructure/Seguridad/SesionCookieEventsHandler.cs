using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace SolarUY.Infrastructure.Seguridad;

public sealed class SesionCookieEventsHandler : CookieAuthenticationEvents
{
    public override Task SigningIn(CookieSigningInContext context)
    {
        context.Properties.Items["solaruy.session"] = Guid.NewGuid().ToString("N");
        return Task.CompletedTask;
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api/asistente"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        return base.RedirectToLogin(context);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api/asistente"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        return base.RedirectToAccessDenied(context);
    }

    private readonly ILogger<SesionCookieEventsHandler> _logger;

    public SesionCookieEventsHandler(ILogger<SesionCookieEventsHandler> logger)
    {
        _logger = logger;
    }

    public override Task SignedIn(CookieSignedInContext context)
    {
        // Diagnóstico optativo: nunca se registran valores de cookies, tokens o identidad.
        var cookies = context.Response.Headers.SetCookie
            .Where(valor => valor?.StartsWith(context.Options.Cookie.Name + "=", StringComparison.Ordinal) == true
                || valor?.StartsWith(context.Options.Cookie.Name + "C", StringComparison.Ordinal) == true)
            .Select(valor => valor!).ToArray();
        var duracion = context.Properties.ExpiresUtc - context.Properties.IssuedUtc;
        _logger.LogDebug("Sesión OIDC emitida. Cookies={CantidadCookies}; Bytes={Bytes}; "
            + "HttpOnly={HttpOnly}; Secure={Secure}; SameSiteLax={SameSiteLax}; "
            + "Persistente={Persistente}; RenovacionDeslizante={RenovacionDeslizante}; "
            + "DuracionMinutos={DuracionMinutos}; Tokens={Tokens}; CookieAnteriorEliminada={CookieAnteriorEliminada}.",
            cookies.Length, cookies.Sum(Encoding.UTF8.GetByteCount),
            cookies.Length > 0 && cookies.All(valor => valor.Contains("; httponly", StringComparison.OrdinalIgnoreCase)),
            cookies.Length > 0 && cookies.All(valor => valor.Contains("; secure", StringComparison.OrdinalIgnoreCase)),
            cookies.Length > 0 && cookies.All(valor => valor.Contains("; samesite=lax", StringComparison.OrdinalIgnoreCase)),
            context.Properties.IsPersistent, context.Options.SlidingExpiration, duracion?.TotalMinutes,
            string.Join(",", context.Properties.GetTokens().Select(token => token.Name)),
            context.Response.Headers.SetCookie.Any(valor =>
                valor?.StartsWith("SolarUY.Sesion=;", StringComparison.Ordinal) == true));
        return Task.CompletedTask;
    }
}
