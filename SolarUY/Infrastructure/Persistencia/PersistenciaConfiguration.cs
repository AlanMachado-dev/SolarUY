using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace SolarUY.Infrastructure.Persistencia;

public static class PersistenciaConfiguration
{
    public static void CargarConfiguracion(IConfigurationBuilder builder, string directorio,
        string entorno, string[] args)
    {
        builder.Sources.Clear();
        builder.SetBasePath(directorio)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{entorno}.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }

    public static string ObtenerEntorno(string[] args)
    {
        var configuracion = new ConfigurationBuilder()
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddEnvironmentVariables("DOTNET_")
            .AddCommandLine(args).Build();
        return configuracion["environment"] ?? Environments.Production;
    }

    public static string ResolverDirectorioProyecto()
    {
        foreach (var inicio in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directorio = new DirectoryInfo(inicio); directorio is not null; directorio = directorio.Parent)
            {
                if (File.Exists(Path.Combine(directorio.FullName, "SolarUY.csproj")))
                    return directorio.FullName;
                var servidor = Path.Combine(directorio.FullName, "SolarUY");
                if (File.Exists(Path.Combine(servidor, "SolarUY.csproj")))
                    return servidor;
            }
        }
        throw new InvalidOperationException("No se encontró el directorio del proyecto servidor SolarUY.");
    }

    public static string ObtenerConexion(IConfiguration configuracion)
    {
        var conexion = configuracion.GetConnectionString("SolarUy");
        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException("Configurá ConnectionStrings:SolarUy antes de iniciar SolarUY o ejecutar migraciones.");
        try
        {
            var opciones = new MySqlConnectionStringBuilder(conexion);
            if (string.IsNullOrWhiteSpace(opciones.Server) || string.IsNullOrWhiteSpace(opciones.Database)
                || string.IsNullOrWhiteSpace(opciones.UserID))
                throw new ArgumentException();
        }
        catch (Exception error) when (error is ArgumentException or FormatException or MySqlException)
        {
            // No incluir la excepción original: puede contener valores de conexión.
            throw new InvalidOperationException("ConnectionStrings:SolarUy no es una conexión MySQL válida; revisá servidor, base, usuario y opciones.");
        }
        return conexion;
    }

    public static void ConfigurarContexto(DbContextOptionsBuilder options, string conexion)
    {
        // Evita que el lector convierta char(36) a Guid antes de nuestra conversión
        // explícita Guid <-> string. No cambia el almacenamiento a binario.
        var opcionesConexion = new MySqlConnectionStringBuilder(conexion) { OldGuids = true };
        options.UseMySQL(opcionesConexion.ConnectionString);
    }
}
