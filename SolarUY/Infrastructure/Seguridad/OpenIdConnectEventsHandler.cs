using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SolarUY.Infrastructure.Seguridad;

public sealed class OpenIdConnectEventsHandler : OpenIdConnectEvents
{
    private readonly ILogger<OpenIdConnectEventsHandler> _logger;

    public OpenIdConnectEventsHandler(ILogger<OpenIdConnectEventsHandler> logger)
    {
        _logger = logger;
    }

    public override Task TokenValidated(TokenValidatedContext context)
    {
        var tipo = context.Options.TokenValidationParameters.RoleClaimType;
        foreach (var identidad in context.Principal!.Identities)
        {
            var claims = identidad.Claims.Where(claim =>
                string.Equals(claim.Type, tipo, StringComparison.OrdinalIgnoreCase)).ToArray();
            var roles = claims.Where(claim => claim.Type == tipo)
                .SelectMany(LeerRoles).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var claim in claims) identidad.RemoveClaim(claim);
            foreach (var rol in roles)
                identidad.AddClaim(new Claim(tipo, rol, ClaimValueTypes.String, context.SecurityToken.Issuer));
        }
        return Task.CompletedTask;
    }

    public override Task TicketReceived(TicketReceivedContext context)
    {
        context.Properties ??= new AuthenticationProperties();
        var token = context.Properties.GetTokenValue("id_token");
        context.Properties.StoreTokens(string.IsNullOrEmpty(token) ? [] :
            [new AuthenticationToken { Name = "id_token", Value = token }]);
        context.Properties.IsPersistent = false;
        context.Properties.AllowRefresh = false;
        context.Properties.IssuedUtc = DateTimeOffset.UtcNow;
        context.Properties.ExpiresUtc = context.Properties.IssuedUtc.Value.AddMinutes(30);
        SeguridadConfiguration.EliminarCookieAnterior(context.HttpContext);
        return Task.CompletedTask;
    }

    public override Task AccessDenied(AccessDeniedContext context)
    {
        context.HandleResponse();
        context.Response.Redirect("/cuenta/iniciar-sesion?error=cancelado");
        return Task.CompletedTask;
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        context.HandleResponse();
        InformarFallo(context.HttpContext);
        return Task.CompletedTask;
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        context.HandleResponse();
        InformarFallo(context.HttpContext);
        return Task.CompletedTask;
    }

    public override Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        context.ProtocolMessage.IdTokenHint = context.Properties.GetParameter<string>("id_token_hint");
        // El token se envía únicamente al endpoint del proveedor descubierto por el middleware.
        // Parameters es transitorio: no duplica el ID token en el state protegido de logout.
        return Task.CompletedTask;
    }

    private void InformarFallo(HttpContext context)
    {
        _logger.LogWarning("No se completó la autenticación externa. Código: oidc_fallo.");
        context.Response.Redirect("/cuenta/iniciar-sesion?error=proveedor");
    }

    private static IEnumerable<string> LeerRoles(Claim claim)
    {
        var valor = claim.Value;
        if (claim.ValueType == ClaimValueTypes.String)
            return valor is "Administrador" or "Operador" ? [valor] : [];
        // Una cadena desconocida con forma de JSON sigue siendo un rol desconocido.
        // Solo se deserializa un array que el lector del token identificó como tal.
        if (claim.ValueType != JsonClaimValueTypes.JsonArray) return [];
        try
        {
            using var documento = JsonDocument.Parse(valor);
            if (documento.RootElement.ValueKind != JsonValueKind.Array) return [];
            return documento.RootElement.EnumerateArray()
                .Where(elemento => elemento.ValueKind == JsonValueKind.String)
                .Select(elemento => elemento.GetString()!)
                .Where(rol => rol is "Administrador" or "Operador").ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
