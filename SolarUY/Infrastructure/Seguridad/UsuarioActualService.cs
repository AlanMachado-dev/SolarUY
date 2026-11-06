using SolarUY.Application.ParquesSolares;
using SolarUY.Application.Asistente;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Cryptography;
using System.Text;

namespace SolarUY.Infrastructure.Seguridad;

public sealed class UsuarioActualService : IUsuarioActualService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public UsuarioActualService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public bool EstaAutenticado => _contextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool PerteneceAlRol(string rol) => EstaAutenticado
        && _contextAccessor.HttpContext!.User.IsInRole(rol);

    public async Task<SesionAsistenteDto?> ObtenerSesionAsistenteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contexto = _contextAccessor.HttpContext;
        if (contexto is null || !EstaAutenticado) return null;
        var resultado = await contexto.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var identidad = resultado.Principal?.FindFirst("sub")?.Value;
        if (!resultado.Succeeded || string.IsNullOrEmpty(identidad)
            || resultado.Properties?.IssuedUtc is not { } emision || resultado.Properties.ExpiresUtc is not { } vencimiento)
            return null;
        var emisor = resultado.Principal?.FindFirst("iss")?.Value ?? "";
        resultado.Properties.Items.TryGetValue("solaruy.session", out var sello);
        var marca = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{emisor}|{identidad}|{emision.UtcTicks}|{vencimiento.UtcTicks}|{sello}")));
        return new(marca, emision, vencimiento);
    }
}
