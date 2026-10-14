namespace SolarUY.Infrastructure.Seguridad;

public sealed class AutenticacionOptions
{
    public List<UsuarioConfigurado> Usuarios { get; set; } = [];
}

public sealed class UsuarioConfigurado
{
    public string Nombre { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}
