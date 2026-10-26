// Comprobaciones auxiliares con MySQL real; no son pruebas unitarias ni aceptación OIDC.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;

internal sealed class ComprobacionException(string criterio) : Exception(criterio);

internal static class Integracion
{
    internal const string Anterior = "20261025172126_AltaNodo";
    internal static int Comprobaciones;
    internal static string BasePrincipal() => JsonSerializer.Deserialize<JsonElement>(
        File.ReadAllText("artifacts/mod002/destino-aislado.json")).GetProperty("Base").GetString()!;

    internal static SolarUyDbContext Contexto(string? destino = null, IInterceptor? interceptor = null)
    {
        destino ??= BasePrincipal();
        if (!destino.StartsWith("solaruy_mod002_", StringComparison.Ordinal)) throw new ComprobacionException("destino-no-aislado");
        var conexion = VerificarMod002.Opciones(); conexion.Database = destino;
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
        if (interceptor is not null) opciones.AddInterceptors(interceptor);
        return new(opciones.Options);
    }

    internal static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new ComprobacionException(criterio);
        Comprobaciones++;
        Console.WriteLine("OK " + criterio);
    }

    internal static async Task Sql(SolarUyDbContext contexto, string sql)
    {
        await contexto.Database.OpenConnectionAsync();
        await using var comando = contexto.Database.GetDbConnection().CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    internal static async Task<string> Escalar(SolarUyDbContext contexto, string sql)
    {
        await contexto.Database.OpenConnectionAsync();
        await using var comando = contexto.Database.GetDbConnection().CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(await comando.ExecuteScalarAsync())!;
    }

    internal static Nodo Grafo(ParqueSolar parque, string inversor, string sensor, string nombre = "Nodo") =>
        parque.AgregarNodo(nombre, -34.9f, -56.2f,
            Inversor.Crear("Inversor", inversor, Sensor.Crear("Sensor", sensor)));

    public static async Task EjecutarAsync()
    {
        await using var contexto = Contexto();
        await contexto.GetService<IMigrator>().MigrateAsync(Anterior);
        var parque = ParqueSolar.Crear("Compatible mod002 " + Guid.NewGuid().ToString("N")[..8], "Destino aislado");
        await new ParqueSolarRepository(contexto).GuardarAsync(parque);
        contexto.ChangeTracker.Clear();
        var nodo = Grafo(parque, "INV-á", "SEN-á");
        await new NodoRepository(contexto).GuardarAsync(nodo);
        await contexto.Database.MigrateAsync();
        var recargado = (await new ParqueSolarRepository(contexto).ObtenerPorIdAsync(parque.ID))!;
        Exigir(recargado.Nodos.Single().ID == nodo.ID
            && recargado.Nodos.Single().Inversor.Sensor.NumeroSerie == "SEN-á", "I16-migracion-conserva-grafo-y-valores");
        foreach (var tabla in new[] { "Inversores", "Sensores" })
        {
            Exigir(await Escalar(contexto, $"SELECT CONCAT(COLUMN_TYPE, '/', COLLATION_NAME) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{tabla}' AND COLUMN_NAME='NumeroSerie'")
                == "varchar(255)/utf8mb4_0900_bin", "I05-I06-columna-" + tabla);
            Exigir(await Escalar(contexto, $"SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{tabla}' AND INDEX_NAME='UX_{tabla}_NumeroSerie' AND NON_UNIQUE=0 AND SUB_PART IS NULL") == "1", "I07-indice-completo-" + tabla);
        }
        Exigir(await Escalar(contexto, "SELECT (_utf8mb4'a' COLLATE utf8mb4_0900_bin = _utf8mb4'A') + (_utf8mb4'a' COLLATE utf8mb4_0900_bin = _utf8mb4'á') + (_utf8mb4'a' COLLATE utf8mb4_0900_bin = _utf8mb4'a ')") == "0", "I02-binaria-sensible-sin-padding");
        var repositorio = new NodoRepository(contexto);
        Exigir(await repositorio.ExisteNumeroSerieInversorAsync("INV-á")
            && !await repositorio.ExisteNumeroSerieInversorAsync("inv-á")
            && !await repositorio.ExisteNumeroSerieInversorAsync("INV-a"), "I03-consulta-misma-igualdad");
        Exigir(await repositorio.ExisteNumeroSerieSensorAsync("SEN-á")
            && !await repositorio.ExisteNumeroSerieSensorAsync("sen-á")
            && !await repositorio.ExisteNumeroSerieSensorAsync("SEN-a"), "I04-consulta-misma-igualdad");
        foreach (var inversor in new[] { true, false })
        {
            var antes = await contexto.Nodos.CountAsync();
            var rechazo = await repositorio.GuardarAsync(Grafo(recargado,
                inversor ? "INV-á" : "nuevo-inv", inversor ? "nuevo-sen" : "SEN-á"));
            Exigir(rechazo == (inversor ? ResultadoGuardadoNodo.ConflictoNumeroSerieInversor : ResultadoGuardadoNodo.ConflictoNumeroSerieSensor), "I19-indice-rechaza-" + inversor);
            Exigir(await contexto.Nodos.CountAsync() == antes && await contexto.Inversores.CountAsync() == antes
                && await contexto.Sensores.CountAsync() == antes && !contexto.ChangeTracker.Entries().Any(), "I20-reversion-y-limpieza-" + inversor);
            var sufijo = Guid.NewGuid().ToString("N");
            Exigir(await repositorio.GuardarAsync(Grafo(recargado, "i" + sufijo, "s" + sufijo)) == ResultadoGuardadoNodo.Guardado, "I20-recuperacion-mismo-contexto-" + inversor);
        }
        await Sql(contexto, "CREATE TRIGGER mod002_fallo BEFORE INSERT ON Sensores FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='fallo tecnico auxiliar'");
        try
        {
            var antes = await contexto.Nodos.CountAsync();
            try { await repositorio.GuardarAsync(Grafo(recargado, "fallo-i", "fallo-s")); throw new ComprobacionException("I21-fallo-no-propagado"); }
            catch (DbUpdateException error) when (error.InnerException is MySqlException mysql && mysql.Number == 1644) { }
            Exigir(await contexto.Nodos.CountAsync() == antes && await contexto.Inversores.CountAsync() == antes
                && await contexto.Sensores.CountAsync() == antes && !contexto.ChangeTracker.Entries().Any(), "I21-fallo-tecnico-propagado-sin-parciales");
        }
        finally { await Sql(contexto, "DROP TRIGGER mod002_fallo"); }
        var eliminado = ParqueSolar.Crear("Eliminar mod002", "Aislado");
        await new ParqueSolarRepository(contexto).GuardarAsync(eliminado);
        contexto.ChangeTracker.Clear();
        await contexto.ParquesSolares.Where(p => p.ID == eliminado.ID).ExecuteDeleteAsync();
        Exigir(await repositorio.GuardarAsync(Grafo(eliminado, "eliminado-i", "eliminado-s")) == ResultadoGuardadoNodo.ParqueNoEncontrado, "I10-parque-eliminado-conservado");
        await VerificarMigracionesAsync();
        await File.WriteAllTextAsync("artifacts/mod002/integracion-resultados.json", JsonSerializer.Serialize(new
        { comprobaciones = Comprobaciones, alcance = "MySQL real, migraciones y repositorio; sin aceptación externa OIDC" }));
    }

    internal static async Task VerificarMigracionesAsync()
    {
        foreach (var tabla in new[] { "Inversores", "Sensores" })
        {
            await MigracionIncompatible(tabla, false);
            await MigracionIncompatible(tabla, false, true);
            await MigracionIncompatible(tabla, true);
        }
    }

    private static async Task MigracionIncompatible(string tabla, bool longitud, bool espacios = false)
    {
        var destino = "solaruy_mod002_" + Guid.NewGuid().ToString("N")[..12];
        var opciones = VerificarMod002.Opciones(); opciones.Database = "";
        await using (var admin = new MySqlConnection(opciones.ConnectionString))
        {
            await admin.OpenAsync(); await using var comando = admin.CreateCommand();
            comando.CommandText = $"CREATE DATABASE `{destino}` CHARACTER SET utf8mb4";
            await comando.ExecuteNonQueryAsync();
        }
        await File.AppendAllTextAsync("artifacts/mod002/bases-auxiliares.txt", destino + Environment.NewLine);
        await using var contexto = Contexto(destino);
        await contexto.GetService<IMigrator>().MigrateAsync(Anterior);
        var parque = ParqueSolar.Crear("Histórico", "Aislado");
        await new ParqueSolarRepository(contexto).GuardarAsync(parque); contexto.ChangeTracker.Clear();
        await new NodoRepository(contexto).GuardarAsync(Grafo(parque, "uno", "uno"));
        await new NodoRepository(contexto).GuardarAsync(Grafo(parque, "dos", "dos"));
        // Datos históricos incompatibles deliberados, solo en la base recién creada.
        await Sql(contexto, longitud ? $"UPDATE `{tabla}` SET NumeroSerie=REPEAT('X',256) WHERE NumeroSerie='dos'"
            : $"UPDATE `{tabla}` SET NumeroSerie='{(espacios ? " \tuno\r\n " : "uno")}' WHERE NumeroSerie='dos'");
        var antes = await Escalar(contexto, $"SELECT GROUP_CONCAT(HEX(NumeroSerie) ORDER BY ID) FROM `{tabla}`");
        try { await contexto.Database.MigrateAsync(); throw new ComprobacionException("migracion-debio-bloquearse"); }
        catch (MySqlException mysql) when (mysql.Number == 3819) { }
        Exigir(antes == await Escalar(contexto, $"SELECT GROUP_CONCAT(HEX(NumeroSerie) ORDER BY ID) FROM `{tabla}`")
            && await contexto.Nodos.CountAsync() == 2 && await contexto.Inversores.CountAsync() == 2 && await contexto.Sensores.CountAsync() == 2,
            "I17-I18-datos-intactos-" + tabla + "-longitud-" + longitud + "-espacios-" + espacios);
        Exigir(await Escalar(contexto, "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Inversores','Sensores') AND COLUMN_NAME='NumeroSerie' AND DATA_TYPE='longtext'") == "2"
            && await Escalar(contexto, "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND INDEX_NAME IN ('UX_Inversores_NumeroSerie','UX_Sensores_NumeroSerie')") == "0",
            "I17-I18-esquema-intacto-" + tabla + "-longitud-" + longitud + "-espacios-" + espacios);
    }
}
