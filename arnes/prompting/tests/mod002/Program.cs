using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Infrastructure.Persistencia;

internal static class VerificarMod002
{
    internal static MySqlConnectionStringBuilder Opciones()
    {
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        return new(PersistenciaConfiguration.ObtenerConexion(configuracion));
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("infra")) { await Integracion.EjecutarAsync(); return 0; }
            if (args.Contains("web")) { await VerificarWeb.EjecutarAsync(args.Contains("preview")); return 0; }
            if (args.Contains("migraciones")) { await Integracion.VerificarMigracionesAsync(); return 0; }
            var opciones = Opciones();
            opciones.Database = "";
            await using var conexion = new MySqlConnection(opciones.ConnectionString);
            await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT VERSION(), PAD_ATTRIBUTE FROM information_schema.COLLATIONS WHERE COLLATION_NAME='utf8mb4_0900_bin'";
            await using (var lector = await comando.ExecuteReaderAsync())
            {
                if (!await lector.ReadAsync() || lector.GetString(1) != "NO PAD") throw new InvalidOperationException("comparacion-incompatible");
                Console.WriteLine("MySQL " + lector.GetString(0) + "; utf8mb4_0900_bin NO PAD");
            }
            var destino = "solaruy_mod002_" + Guid.NewGuid().ToString("N")[..12];
            comando.CommandText = $"CREATE DATABASE `{destino}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
            await comando.ExecuteNonQueryAsync();
            await File.WriteAllTextAsync("artifacts/mod002/destino-aislado.json", JsonSerializer.Serialize(new { Base = destino }));
            Console.WriteLine("Destino aislado creado: " + destino);
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("Fallo depurado: " + error.GetBaseException().GetType().Name
                + "; código=" + (error.GetBaseException() is MySqlException mysql ? mysql.Number : 0)
                + "; origen=" + error.TargetSite?.Name);
            if (error is ComprobacionException) Console.WriteLine(error.Message);
            Console.WriteLine(error.StackTrace);
            if (error.GetBaseException() is MySqlException detalle)
                Console.WriteLine("Índice: " + System.Text.RegularExpressions.Regex.Match(detalle.Message, " for key '([^']*)'$").Groups[1].Value);
            return 1;
        }
    }
}
