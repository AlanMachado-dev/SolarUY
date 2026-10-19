// Ejecutable de aceptación funcional. El arnés crea su proyecto en artifacts/req002.
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;

internal static class VerificarPersistencia
{
    private static string Conexion => Environment.GetEnvironmentVariable("ConnectionStrings__SolarUy")
        ?? throw new InvalidOperationException("Falta conexión de aceptación.");
    private static string Base => Environment.GetEnvironmentVariable("SOLARUY_ACEPTACION_BASE") ?? "";

    public static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            object resultado = args[0] == "configuracion"
                ? VerificarConfiguracion()
                : await EjecutarAsync(args);
            Console.WriteLine(JsonSerializer.Serialize(new { correcto = true, resultado }));
            return 0;
        }
        catch (Exception error)
        {
            // Los mensajes del conector pueden contener SQL, valores o secretos.
            var causa = error.GetBaseException();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                correcto = false, tipo = causa.GetType().Name,
                codigo = causa is MySqlException mysql ? mysql.Number : 0,
                detalleConversion = causa is InvalidCastException ? causa.Message : null
            }));
            return 1;
        }
    }

    private static void Exigir(bool condicion)
    {
        if (!condicion) throw new InvalidOperationException("Falló una comprobación funcional.");
    }

    private static void ValidarDestino()
    {
        Exigir(Regex.IsMatch(Base, "^solaruy_req002_aceptacion_[a-f0-9]{12}$"));
        Exigir(new MySqlConnectionStringBuilder(Conexion).Database == Base);
    }

    private static SolarUyDbContext Contexto(string? conexion = null)
    {
        var options = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(options, conexion ?? Conexion);
        return new SolarUyDbContext(options.Options);
    }

    private static async Task<object> EjecutarAsync(string[] args)
    {
        if (args[0] == "fabrica")
        {
            Exigir(Regex.IsMatch(Base, "^solaruy_req002_aceptacion_[a-f0-9]{12}$"));
            await using var contexto = new SolarUyDbContextFactory().CreateDbContext(args.Skip(1).ToArray());
            Exigir(contexto.Database.GetDbConnection().Database == Base);
            await contexto.Database.OpenConnectionAsync();
            return new { baseConectada = contexto.Database.GetDbConnection().Database };
        }
        ValidarDestino();
        if (args[0] == "secundario")
        {
            var opciones = new MySqlConnectionStringBuilder(Conexion)
            {
                Database = "solaruy_req002_aceptacion_" + Guid.NewGuid().ToString("N")[..12]
            };
            Environment.SetEnvironmentVariable("ConnectionStrings__SolarUy", opciones.ConnectionString);
            Environment.SetEnvironmentVariable("SOLARUY_ACEPTACION_BASE", opciones.Database);
            await EjecutarAsync(["preparar"]);
            await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { conexion = opciones.ConnectionString, baseDatos = opciones.Database }));
            return new { creada = opciones.Database };
        }
        if (args[0] == "preparar")
        {
            var opciones = new MySqlConnectionStringBuilder(Conexion) { Database = "" };
            await using var admin = new MySqlConnection(opciones.ConnectionString);
            await admin.OpenAsync();
            await using var existe = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @base", admin);
            existe.Parameters.AddWithValue("@base", Base);
            Exigir(Convert.ToInt32(await existe.ExecuteScalarAsync()) == 0);
            await using var crear = new MySqlCommand($"CREATE DATABASE `{Base}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin", admin);
            await crear.ExecuteNonQueryAsync();
            return new { creada = Base, version = admin.ServerVersion };
        }
        if (args[0] == "limpiar")
        {
            // Solo se invoca cuando el proceso padre registró la creación en esta ejecución.
            await using var conexion = new MySqlConnection(Conexion);
            await conexion.OpenAsync();
            if (args.Length > 1)
            {
                Exigir(Regex.IsMatch(args[1], "^su_req002_[a-f0-9]{12}$"));
                var administrativa = Environment.GetEnvironmentVariable("SOLARUY_ACEPTACION_ADMIN_CONNECTION") ?? Conexion;
                await using var admin = new MySqlConnection(administrativa);
                await admin.OpenAsync();
                await using var usuario = new MySqlCommand($"DROP USER '{args[1]}'@'localhost'", admin);
                await usuario.ExecuteNonQueryAsync();
            }
            await using var borrar = new MySqlCommand($"DROP DATABASE `{Base}`", conexion);
            await borrar.ExecuteNonQueryAsync();
            return new { eliminada = Base };
        }
        if (args[0] == "repositorio") return await VerificarRepositorioAsync();
        if (args[0] == "permisos") return await PrepararPermisosAsync(args[1]);
        if (args[0] == "fallo-escritura")
        {
            await using var conexion = new MySqlConnection(Conexion);
            await conexion.OpenAsync();
            var sql = args[1] == "activar"
                ? "CREATE TRIGGER req002_rechazo BEFORE INSERT ON ParquesSolares FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req002 rechazo controlado'"
                : "DROP TRIGGER req002_rechazo";
            await using var comando = new MySqlCommand(sql, conexion);
            await comando.ExecuteNonQueryAsync();
            return new { activo = args[1] == "activar" };
        }
        await using var db = Contexto();
        await db.Database.OpenConnectionAsync();
        if (args[0] == "consulta")
        {
            var nombre = Console.In.ReadToEnd();
            var clave = new NombreParqueSolar(nombre).Clave;
            var parques = await db.ParquesSolares.AsNoTracking()
                .Where(parque => parque.Nombre.Clave == clave).ToListAsync();
            return parques.Select(p => new { p.ID, Nombre = p.Nombre.Valor, p.Nombre.Clave, p.Ubicacion });
        }
        if (args[0] == "conteo") return await db.ParquesSolares.CountAsync();
        if (args[0] == "esquema")
        {
            var mysql = (MySqlConnection)db.Database.GetDbConnection();
            var tablas = await LeerAsync(mysql,
                "SELECT TABLE_NAME, ENGINE, TABLE_COLLATION FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME");
            var columnas = await LeerAsync(mysql,
                "SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, CHARACTER_SET_NAME, COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ParquesSolares' ORDER BY ORDINAL_POSITION");
            var indices = await LeerAsync(mysql,
                "SELECT INDEX_NAME, NON_UNIQUE, COLUMN_NAME, SUB_PART FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ParquesSolares' ORDER BY INDEX_NAME");
            Exigir(tablas.Count == 2 && tablas.Any(t => string.Equals(t[0], "ParquesSolares", StringComparison.OrdinalIgnoreCase) && t[1] == "InnoDB"));
            Exigir(columnas.Count == 4 && columnas.All(c => c[2] == "NO"));
            Exigir(columnas.Any(c => c[0] == "ID" && c[1] == "char(36)"));
            Exigir(columnas.Any(c => c[0] == "Nombre" && c[1] == "varchar(100)" && c[3] == "utf8mb4"));
            Exigir(columnas.Any(c => c[0] == "ClaveNombre" && c[1] == "varchar(100)" && c[4] == "utf8mb4_0900_bin"));
            Exigir(columnas.Any(c => c[0] == "Ubicacion" && c[1] == "longtext"));
            Exigir(indices.Any(i => i[0] == "UX_ParquesSolares_ClaveNombre" && i[1] == "0" && i[2] == "ClaveNombre" && i[3] == null));
            var historial = await db.Database.GetAppliedMigrationsAsync();
            Exigir(historial.Count() == 1);
            return new { version = mysql.ServerVersion, tablas, columnas, indices, historial };
        }
        throw new InvalidOperationException("Comando desconocido.");
    }

    private static async Task<List<string?[]>> LeerAsync(MySqlConnection conexion, string sql)
    {
        await using var comando = new MySqlCommand(sql, conexion);
        await using var reader = await comando.ExecuteReaderAsync();
        var filas = new List<string?[]>();
        while (await reader.ReadAsync())
            filas.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i).ToString()).ToArray());
        return filas;
    }

    private static async Task<object> VerificarRepositorioAsync()
    {
        var nombre = "Repositorio " + Guid.NewGuid().ToString("N");
        var parque = ParqueSolar.Crear(nombre, "  Montevideo  ");
        await using (var db = Contexto())
            Exigir(await new ParqueSolarRepository(db).GuardarAsync(parque) == ResultadoGuardadoParqueSolar.Guardado);
        await using (var db = Contexto())
        {
            Exigir(await new ParqueSolarRepository(db).ExisteNombreAsync(parque.Nombre.Clave));
            var leido = await db.ParquesSolares.SingleAsync(p => p.ID == parque.ID);
            Exigir(leido.ID == parque.ID && leido.Nombre.Valor == nombre && leido.Ubicacion == "Montevideo");
        }
        await using (var db = Contexto())
        {
            var duplicado = ParqueSolar.Crear("  " + nombre.ToLowerInvariant() + "  ", "Salto");
            Exigir(await new ParqueSolarRepository(db).GuardarAsync(duplicado) == ResultadoGuardadoParqueSolar.ConflictoNombre);
            Exigir(!db.ChangeTracker.Entries().Any());
            Exigir(await new ParqueSolarRepository(db).GuardarAsync(ParqueSolar.Crear(nombre + " nuevo", "Salto")) == ResultadoGuardadoParqueSolar.Guardado);
        }
        await using (var db = Contexto())
        {
            var colision = ParqueSolar.Crear(nombre + " otra clave", "Salto");
            typeof(ParqueSolar).GetProperty(nameof(ParqueSolar.ID))!.SetValue(colision, parque.ID);
            try { await new ParqueSolarRepository(db).GuardarAsync(colision); throw new InvalidOperationException(); }
            catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 }) { }
        }
        var concurrente = nombre + " carrera";
        await using var uno = Contexto();
        await using var dos = Contexto();
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ResultadoGuardadoParqueSolar> Guardar(SolarUyDbContext db, string texto)
        {
            await salida.Task;
            return await new ParqueSolarRepository(db).GuardarAsync(ParqueSolar.Crear(texto, "Salto"));
        }
        var primero = Guardar(uno, concurrente);
        var segundo = Guardar(dos, concurrente.ToUpperInvariant());
        salida.SetResult();
        var resultados = await Task.WhenAll(primero, segundo);
        Exigir(resultados.Count(r => r == ResultadoGuardadoParqueSolar.Guardado) == 1);
        Exigir(resultados.Count(r => r == ResultadoGuardadoParqueSolar.ConflictoNombre) == 1);
        Exigir(await uno.ParquesSolares.CountAsync(p => p.Nombre.Clave == new NombreParqueSolar(concurrente).Clave) == 1);
        return new { lectura = true, guid = true, conflicto = true, seguimiento = true, clavePrimariaPropagada = true, concurrencia = true };
    }

    private static async Task<object> PrepararPermisosAsync(string archivoPrivado)
    {
        var lectura = Environment.GetEnvironmentVariable("SOLARUY_ACEPTACION_READONLY_CONNECTION");
        if (!string.IsNullOrWhiteSpace(lectura))
        {
            var opcionesLectura = new MySqlConnectionStringBuilder(lectura) { Database = Base };
            await using var contexto = Contexto(opcionesLectura.ConnectionString);
            Exigir(!await new ParqueSolarRepository(contexto).ExisteNombreAsync("SIN PERMISOS"));
            try
            {
                await new ParqueSolarRepository(contexto).GuardarAsync(ParqueSolar.Crear("Sin permisos", "Salto"));
                throw new InvalidOperationException();
            }
            catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1142 or 1143 }) { }
            await File.WriteAllTextAsync(archivoPrivado, JsonSerializer.Serialize(new { conexion = opcionesLectura.ConnectionString, usuario = (string?)null }));
            return new { cuentaExistenteSinModificar = true, escrituraDenegada = true };
        }
        var usuario = "su_req002_" + Base[^12..];
        var password = Guid.NewGuid().ToString("N") + "Aa1!";
        var administrativa = Environment.GetEnvironmentVariable("SOLARUY_ACEPTACION_ADMIN_CONNECTION") ?? Conexion;
        await using var conexion = new MySqlConnection(administrativa);
        await conexion.OpenAsync();
        await using var crear = new MySqlCommand($"CREATE USER '{usuario}'@'localhost' IDENTIFIED BY @password", conexion);
        crear.Parameters.AddWithValue("@password", password);
        await crear.ExecuteNonQueryAsync();
        try
        {
            await using var permiso = new MySqlCommand($"GRANT SELECT ON `{Base}`.* TO '{usuario}'@'localhost'", conexion);
            await permiso.ExecuteNonQueryAsync();
            var opciones = new MySqlConnectionStringBuilder(Conexion) { UserID = usuario, Password = password };
            await File.WriteAllTextAsync(archivoPrivado, JsonSerializer.Serialize(new { conexion = opciones.ConnectionString, usuario }));
            await using var db = Contexto(opciones.ConnectionString);
            Exigir(!await new ParqueSolarRepository(db).ExisteNombreAsync("SIN PERMISOS"));
            try
            {
                await new ParqueSolarRepository(db).GuardarAsync(ParqueSolar.Crear("Sin permisos", "Salto"));
                throw new InvalidOperationException();
            }
            catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1142 }) { }
            return new { usuario, escrituraDenegada = true };
        }
        catch
        {
            await using var borrar = new MySqlCommand($"DROP USER '{usuario}'@'localhost'", conexion);
            await borrar.ExecuteNonQueryAsync();
            throw;
        }
    }

    private static object VerificarConfiguracion()
    {
        foreach (var conexion in new string?[] { null, "", "   ", "no es una cadena", "Server=localhost;Port=incorrecto;", "Server=x;Database=base;User ID=u;OpcionInventada=secreto;" })
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:SolarUy"] = conexion }).Build();
            try { PersistenciaConfiguration.ObtenerConexion(config); throw new Exception("Se aceptó conexión inválida."); }
            catch (InvalidOperationException error) { Exigir(!error.Message.Contains("secreto")); }
        }
        var original = Environment.GetEnvironmentVariable("ConnectionStrings__SolarUy");
        var carpeta = Path.Combine(Path.GetTempPath(), "solaruy-req002-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            File.WriteAllText(Path.Combine(carpeta, "appsettings.json"), "{\"Orden\":\"base\"}");
            File.WriteAllText(Path.Combine(carpeta, "appsettings.Aceptacion.json"), "{\"Orden\":\"entorno\"}");
            File.WriteAllText(Path.Combine(carpeta, "appsettings.Local.json"), "{\"Orden\":\"local\"}");
            var config = new ConfigurationBuilder();
            PersistenciaConfiguration.CargarConfiguracion(config, carpeta, "Aceptacion", []);
            Exigir(config.Build()["Orden"] == "local");
            Environment.SetEnvironmentVariable("ConnectionStrings__SolarUy", "Server=localhost;Database=env;User ID=u;");
            PersistenciaConfiguration.CargarConfiguracion(config, carpeta, "Aceptacion", []);
            Exigir(new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(config.Build())).Database == "env");
            PersistenciaConfiguration.CargarConfiguracion(config, carpeta, "Aceptacion", ["--ConnectionStrings:SolarUy=Server=localhost;Database=argumentos;User ID=u;"]);
            Exigir(new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(config.Build())).Database == "argumentos");
            Exigir(PersistenciaConfiguration.ObtenerEntorno(["--environment", "Aceptacion"]) == "Aceptacion");
            File.Delete(Path.Combine(carpeta, "appsettings.Local.json"));
            PersistenciaConfiguration.CargarConfiguracion(config, carpeta, "Aceptacion", []);
            Exigir(config.Build()["Orden"] == "entorno");
            File.Delete(Path.Combine(carpeta, "appsettings.Aceptacion.json"));
            PersistenciaConfiguration.CargarConfiguracion(config, carpeta, "Aceptacion", []);
            Exigir(config.Build()["Orden"] == "base");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__SolarUy", original);
            foreach (var archivo in Directory.GetFiles(carpeta)) File.Delete(archivo);
            Directory.Delete(carpeta);
        }
        return new { sintaxis = true, precedencia = true, localOpcional = true, entorno = true };
    }
}
