using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SolarUY.Infrastructure.Seguridad;

public sealed class CredencialesService
{
    private readonly AutenticacionOptions _options;
    private readonly IPasswordHasher<UsuarioConfigurado> _passwordHasher;

    public CredencialesService(IOptions<AutenticacionOptions> options,
        IPasswordHasher<UsuarioConfigurado> passwordHasher)
    {
        _options = options.Value;
        _passwordHasher = passwordHasher;
    }

    public ClaimsPrincipal? Validar(string nombre, string password)
    {
        var usuario = _options.Usuarios.SingleOrDefault(
            candidato => string.Equals(candidato.Nombre, nombre, StringComparison.Ordinal));
        if (usuario is null || _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password)
            == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, usuario.Nombre) };
        claims.AddRange(usuario.Roles.Select(rol => new Claim(ClaimTypes.Role, rol)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
