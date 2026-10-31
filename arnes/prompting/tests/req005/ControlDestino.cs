using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Infrastructure.Persistencia;

// Solo opera en la base auxiliar registrada y validada; nunca modifica la configuración privada.
internal static class ControlDestino
{
    public static async Task<int> EjecutarAsync(string modo)
    {
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var opciones = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var habitual = opciones.Database;
        var destino = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync("artifacts/req005/destino-aislado.json"))
            .GetProperty("Base").GetString()!;
        if (destino == habitual || !Regex.IsMatch(destino, "^solaruy_req005_[a-f0-9]{12}$"))
            throw new InvalidOperationException("Destino aislado inválido.");
        opciones.Database = destino;
        await using var conexion = new MySqlConnection(opciones.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        if (modo == "limpiar")
        {
            comando.CommandText = "SHOW DATABASES LIKE 'solaruy_req005_%'";
            var candidatos = new List<string>();
            await using (var lector = await comando.ExecuteReaderAsync())
                while (await lector.ReadAsync()) candidatos.Add(lector.GetString(0));
            var retirados = new List<string>();
            foreach (var candidato in candidatos)
            {
                if (candidato == habitual || !Regex.IsMatch(candidato, "^solaruy_req005_[a-f0-9]{12}$")) continue;
                // Se exige la huella semántica completa de los datos creados por este auxiliar.
                comando.CommandText = $"SELECT COUNT(*) FROM `{candidato}`.ParquesSolares";
                if (Convert.ToInt32(await comando.ExecuteScalarAsync()) != 3) continue;
                comando.CommandText = $"SELECT COUNT(*) FROM `{candidato}`.ParquesSolares WHERE Ubicacion IN ('Destino aislado A', 'Destino aislado B', 'Destino aislado sin nodos')";
                if (Convert.ToInt32(await comando.ExecuteScalarAsync()) != 3) continue;
                comando.CommandText = $"SELECT COUNT(*) FROM `{candidato}`.Nodos WHERE Nombre NOT LIKE 'Nodo req005 %' AND Nombre NOT IN ('Nodo alta regresión req005', 'Nodo regresión OIDC req005')";
                if (Convert.ToInt32(await comando.ExecuteScalarAsync()) != 0) continue;
                comando.CommandText = $"DROP DATABASE `{candidato}`";
                await comando.ExecuteNonQueryAsync();
                retirados.Add(candidato);
            }
            await File.WriteAllTextAsync("artifacts/req005/recursos-retirados.json", JsonSerializer.Serialize(retirados));
            Console.WriteLine($"Bases auxiliares verificadas y retiradas: {retirados.Count}.");
            return 0;
        }
        if (modo is "activar-fallo" or "restaurar")
        {
            // Validación adicional de pertenencia antes de provocar el fallo en la tabla aislada.
            comando.CommandText = "SELECT COUNT(*) FROM ParquesSolares WHERE Ubicacion LIKE 'Destino aislado%'";
            if (Convert.ToInt32(await comando.ExecuteScalarAsync()) != 3) throw new InvalidOperationException("Datos no reconocidos.");
            comando.CommandText = modo == "activar-fallo"
                ? "RENAME TABLE Nodos TO Nodos_req005_fallo"
                : "RENAME TABLE Nodos_req005_fallo TO Nodos";
            await comando.ExecuteNonQueryAsync();
            Console.WriteLine(modo == "activar-fallo" ? "Fallo de consulta activado exclusivamente en req005." : "Tabla aislada restaurada.");
            return 0;
        }
        if (modo is "estado-antes" or "estado-despues")
        {
            var contenido = new StringBuilder();
            foreach (var tabla in new[] { "ParquesSolares", "Nodos", "Inversores", "Sensores" })
            {
                comando.CommandText = $"SELECT * FROM `{tabla}` ORDER BY ID";
                await using var lector = await comando.ExecuteReaderAsync();
                while (await lector.ReadAsync())
                    for (var campo = 0; campo < lector.FieldCount; campo++)
                        contenido.Append(JsonSerializer.Serialize(lector.GetValue(campo))).Append('\n');
            }
            var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido.ToString())));
            if (modo == "estado-antes") await File.WriteAllTextAsync("artifacts/req005/huella-antes.txt", huella);
            else
            {
                if (huella != await File.ReadAllTextAsync("artifacts/req005/huella-antes.txt"))
                    throw new InvalidOperationException("La persistencia cambió durante las consultas.");
                await File.WriteAllTextAsync("artifacts/req005/solo-lectura-real.json", "{\"sinCambiosPersistidos\":true}");
            }
            Console.WriteLine(modo == "estado-antes" ? "Huella inicial guardada sin datos privados." : "Sin cambios persistidos durante la consulta real.");
            return 0;
        }
        throw new InvalidOperationException("Modo no reconocido.");
    }
}
