using System.Net;
using System.Text.RegularExpressions;

namespace SolarUY.Infrastructure.Balanceo;

public sealed class BalanceoOptions
{
    public bool Habilitado { get; set; }
    public string Instancia { get; set; } = "local";
    public string ProxyConfiable { get; set; } = string.Empty;
    public string AutoridadPublica { get; set; } = "localhost:8080";
    public string DirectorioClaves { get; set; } = string.Empty;
    public string CertificadoClaves { get; set; } = string.Empty;
    public string PasswordCertificadoFile { get; set; } = string.Empty;
    public double IntervaloSincronizacionSegundos { get; set; } = 1;

    public bool EsValido() => !Habilitado ||
        (Regex.IsMatch(Instancia, "^[a-zA-Z0-9_-]{1,40}$") &&
        IPAddress.TryParse(ProxyConfiable, out _) &&
        Uri.TryCreate($"https://{AutoridadPublica}/", UriKind.Absolute, out var origen) &&
        origen.Authority == AutoridadPublica && string.IsNullOrEmpty(origen.UserInfo) &&
        Directory.Exists(DirectorioClaves) && File.Exists(CertificadoClaves) &&
        File.Exists(PasswordCertificadoFile) &&
        double.IsFinite(IntervaloSincronizacionSegundos) && IntervaloSincronizacionSegundos is >= 0.1 and <= 60);
}
