using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace SolarUY.Infrastructure.Balanceo;

public static class BalanceoConfiguration
{
    public static BalanceoOptions AgregarBalanceo(IServiceCollection servicios, IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection("Balanceo").Get<BalanceoOptions>() ?? new BalanceoOptions();
        if (!opciones.EsValido())
            throw new InvalidOperationException("Configuración de balanceo incompleta: revisá instancia, proxy, origen, claves, certificado e intervalo.");

        servicios.AddSingleton(opciones);
        servicios.AddHealthChecks().AddCheck<DisponibilidadMySqlRepository>("mysql", timeout: TimeSpan.FromSeconds(3));
        if (!opciones.Habilitado)
            return opciones;

        servicios.Configure<ForwardedHeadersOptions>(cabeceras =>
        {
            cabeceras.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            cabeceras.ForwardLimit = 1;
            cabeceras.KnownProxies.Clear();
            cabeceras.KnownIPNetworks.Clear();
            cabeceras.KnownProxies.Add(IPAddress.Parse(opciones.ProxyConfiable));
            cabeceras.AllowedHosts = [new Uri($"https://{opciones.AutoridadPublica}/").Host];
        });
        servicios.AddDataProtection().SetApplicationName("SolarUY")
            .PersistKeysToFileSystem(new DirectoryInfo(opciones.DirectorioClaves))
            .ProtectKeysWithCertificate(CargarCertificado(opciones));
        return opciones;
    }

    private static X509Certificate2 CargarCertificado(BalanceoOptions opciones)
    {
        try
        {
            var password = File.ReadAllText(opciones.PasswordCertificadoFile).TrimEnd('\r', '\n');
            var certificado = X509CertificateLoader.LoadPkcs12FromFile(
                opciones.CertificadoClaves, password, X509KeyStorageFlags.EphemeralKeySet);
            if (certificado.HasPrivateKey && certificado.NotAfter.ToUniversalTime() > DateTime.UtcNow &&
                certificado.NotBefore.ToUniversalTime() <= DateTime.UtcNow)
                return certificado;
            certificado.Dispose();
        }
        catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException)
        {
            // No propagar detalles del certificado, su ruta o la contraseña.
        }
        throw new InvalidOperationException("No se pudo cargar el certificado privado de protección de claves.");
    }

    public static void UsarOrigenPublico(WebApplication app, BalanceoOptions opciones)
    {
        if (!opciones.Habilitado)
            return;
        app.UseForwardedHeaders();
        app.Use(async (contexto, siguiente) =>
        {
            if (!contexto.Request.Path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(contexto.Request.Host.Value, opciones.AutoridadPublica, StringComparison.OrdinalIgnoreCase))
            {
                contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
                await contexto.Response.WriteAsync("Origen no admitido.");
                return;
            }
            await siguiente(contexto);
        });
    }
}
