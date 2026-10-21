namespace SolarUY.Infrastructure.Seguridad;

public sealed class AutenticacionExternaOptions
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RolesClaimType { get; set; } = "https://solaruy.example/roles";
}
