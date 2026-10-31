// Integración auxiliar real con MySQL. No acredita sesiones externas OIDC.
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.Paneles;
using SolarUY.Application.Paneles.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;

internal static class VerificarAltaPanel
{
    internal const string Carpeta = "artifacts/req006";
    internal static readonly List<string> Resultados = [];

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("real"))
            {
                await IniciarAplicacionRealAsync();
                return 0;
            }
            if (args.Contains("final"))
            {
                await VerificacionFinalAsync();
                return 0;
            }
            if (args.Contains("ssr"))
            {
                await VerificarSsr.EjecutarAsync(args.Contains("preview"));
                return 0;
            }
            await IntegracionAsync();
            await File.WriteAllLinesAsync(Carpeta + "/integracion-resultados.txt", Resultados);
            return 0;
        }
        catch (Exception error)
        {
            // No registrar mensajes del proveedor, SQL ni conexiones.
            Console.WriteLine("FALLO " + error.GetBaseException().GetType().Name
                + (error is CriterioFallidoException ? ": " + error.Message : "")
                + " origen=" + error.TargetSite?.DeclaringType?.Name + "." + error.TargetSite?.Name);
            return 1;
        }
    }

    internal static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new CriterioFallidoException(criterio);
        Resultados.Add("OK " + criterio);
        Console.WriteLine("OK " + criterio);
    }

    internal static string ConexionAislada()
    {
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var destino = JsonDocument.Parse(File.ReadAllText(Carpeta + "/destino-aislado.json")).RootElement.GetProperty("Base").GetString()!;
        Exigir(Regex.IsMatch(destino, "^solaruy_req006_[a-f0-9]{12}$") && destino != conexion.Database, "destino-exclusivo-req006");
        conexion.Database = destino;
        return conexion.ConnectionString;
    }

    internal static SolarUyDbContext Contexto(string conexion, IInterceptor? interceptor = null)
    {
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, conexion);
        if (interceptor is not null) opciones.AddInterceptors(interceptor);
        return new SolarUyDbContext(opciones.Options);
    }

    internal static IUsuarioActualService Usuario(string? rol) => new UsuarioAuxiliar(rol);

    internal static PanelService Servicio(SolarUyDbContext contexto) => new(new NodoRepository(contexto),
        new PanelRepository(contexto), Usuario("Administrador"), new RelojFijo());

    internal static CrearPanelDto Datos(string nombre = "Panel Á") => new()
    { Nombre = " " + nombre + " ", Modelo = " Modelo libre ", PotenciaNominalW = 550.5f };

    private static async Task IntegracionAsync()
    {
        Directory.CreateDirectory(Carpeta);
        var conexion = ConexionAislada();
        var opciones = new MySqlConnectionStringBuilder(conexion);
        var destino = opciones.Database;
        opciones.Database = "";
        await using (var admin = new MySqlConnection(opciones.ConnectionString))
        {
            await admin.OpenAsync();
            await using var comando = admin.CreateCommand();
            comando.CommandText = $"CREATE DATABASE `{destino}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
            await comando.ExecuteNonQueryAsync();
        }
        await using var contexto = Contexto(conexion);
        await contexto.GetService<IMigrator>().MigrateAsync("20261026193044_SeriesEquiposUnicas");
        var parque = ParqueSolar.Crear("Parque req006 A", "Destino aislado");
        var otro = ParqueSolar.Crear("Parque req006 B", "Destino aislado");
        var nodo = AgregarNodo(parque, "A");
        var segundo = AgregarNodo(parque, "B");
        var ajeno = AgregarNodo(otro, "C");
        contexto.AddRange(parque, otro);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        await contexto.Database.MigrateAsync();
        Exigir(await contexto.ParquesSolares.CountAsync() == 2 && await contexto.Nodos.CountAsync() == 3
            && await contexto.Inversores.CountAsync() == 3 && await contexto.Sensores.CountAsync() == 3
            && await contexto.Paneles.CountAsync() == 0, "migracion-preserva-datos-previos-sin-paneles");
        var tablas = new List<string>();
        await using (var sql = new MySqlConnection(conexion))
        {
            await sql.OpenAsync();
            await using var comando = sql.CreateCommand();
            comando.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Paneles'";
            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync()) tablas.Add(lector.GetString(0));
        }
        Exigir(tablas.Order().SequenceEqual(new[] { "ID", "IdNodo", "Nombre", "ClaveNombre", "Modelo", "PotenciaNominalW", "FechaInstalacion", "Estado" }.Order()),
            "esquema-solo-paneles-fk-compartida-sin-derivados");
        foreach (var rol in new string?[] { null, "Operador", "administrador", "" })
        {
            var prohibido = new RepositorioProhibido();
            var resultado = await new PanelService(prohibido, prohibido, Usuario(rol), TimeProvider.System).CrearAsync(nodo.ID, Datos());
            Exigir(resultado.Panel is null && resultado.Errores.Single().Codigo == (rol is null
                ? CodigoErrorAltaPanel.AutenticacionRequerida : CodigoErrorAltaPanel.AccesoDenegado), "autorizacion-previa-repositorios-" + (rol ?? "anonimo"));
        }
        var servicio = Servicio(contexto);
        foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
            Exigir((await servicio.CrearAsync(id, Datos())).Errores.Single().Codigo == CodigoErrorAltaPanel.NodoNoEncontrado, "nodo-inexistente");
        foreach (var propiedad in typeof(CrearPanelDto).GetProperties())
        {
            foreach (var valor in propiedad.PropertyType == typeof(string) ? new object?[] { null, "", "   " } : new object?[] { null, 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var datos = Datos(); propiedad.SetValue(datos, valor);
                var resultado = await servicio.CrearAsync(nodo.ID, datos);
                Exigir(resultado.Panel is null && resultado.Errores.Single().Codigo == CodigoErrorAltaPanel.DatosInvalidos
                    && resultado.Errores.Single().Campo == propiedad.Name, "validacion-" + propiedad.Name);
            }
        }
        Exigir(await contexto.Paneles.CountAsync() == 0, "rechazos-sin-escrituras");
        var alta = await servicio.CrearAsync(nodo.ID, Datos());
        Exigir(alta.EsExitoso && alta.Panel!.ID != Guid.Empty && alta.Panel.NodoId == nodo.ID, "alta-identidad-pertenencia");
        await using (var lectura = Contexto(conexion))
        {
            var recargado = await new NodoRepository(lectura).ObtenerPorIdAsync(nodo.ID);
            var panel = recargado!.Paneles.Single();
            Exigir(panel.ID == alta.Panel!.ID && ReferenceEquals(panel.Nodo, recargado) && panel.Nombre.Valor == "Panel Á"
                && panel.Modelo == "Modelo libre" && panel.PotenciaNominalW == 550.5f
                && panel.Estado == EstadoPanel.Operativo && panel.FechaInstalacion == RelojFijo.Instante.UtcDateTime.AddTicks(-1_234_567)
                && panel.FechaInstalacion.Kind == DateTimeKind.Utc && panel.GeneracionActualW is null && panel.UltimoMantenimiento is null,
                "lectura-nueva-solicitud-valores-utc-segundos-derivados");
            Exigir(!lectura.ChangeTracker.Entries().Any(), "lectura-sin-seguimiento");
        }
        Exigir((await servicio.CrearAsync(nodo.ID, Datos("panel a\u0301"))).Errores.Single().Codigo == CodigoErrorAltaPanel.NombreDuplicado,
            "duplicado-normalizado-en-dominio");
        await ConcurrenciaAsync(conexion, nodo.ID, "Concurrente", " concurrente ", true);
        await ConcurrenciaAsync(conexion, nodo.ID, "Diferente A", "Diferente B", false);
        Exigir((await servicio.CrearAsync(segundo.ID, Datos())).EsExitoso && (await servicio.CrearAsync(ajeno.ID, Datos())).EsExitoso,
            "mismo-nombre-en-otros-nodos-y-parques");
        await using (var lectura = Contexto(conexion))
        {
            var nodos = new NodoService(new NodoRepository(lectura), new ParqueSolarRepository(lectura), Usuario("Administrador"));
            var detalle = await nodos.ObtenerPorIdAsync(nodo.ID);
            Exigir(detalle.Paneles.Count == 4 && detalle.Paneles.All(panel => panel.NodoId == nodo.ID)
                && detalle.Paneles.SequenceEqual(detalle.Paneles.OrderBy(panel => panel.Nombre).ThenBy(panel => panel.ID)), "detalle-propio-ordenado");
            var parques = new ParqueSolarService(new ParqueSolarRepository(lectura), Usuario("Administrador"));
            var detalleParque = await parques.ObtenerPorIdAsync(parque.ID);
            var listado = await parques.ListarAsync();
            Exigir(detalleParque.Nodos.Sum(n => n.CantidadPaneles) == 5 && detalleParque.Parque!.CapacidadTotalEstimadaMw == 0.0027525m
                && listado.Parques.Single(p => p.ID == parque.ID).CapacidadTotalEstimadaMw == 0.0027525m
                && listado.Parques.Single(p => p.ID == otro.ID).CapacidadTotalEstimadaMw == 0.0005505m, "dto-cantidades-capacidad-varios-parques");
            foreach (var rol in new string?[] { null, "Operador" })
            {
                var rechazo = await new NodoService(new RepositorioProhibido(), new ParqueSolarRepository(lectura), Usuario(rol)).ObtenerPorIdAsync(nodo.ID);
                Exigir(rechazo.Nodo is null && rechazo.Paneles.Count == 0, "consulta-rechazada-sin-datos");
            }
        }
        await using (var sql = new MySqlConnection(conexion))
        {
            await sql.OpenAsync();
            await using var trigger = sql.CreateCommand();
            trigger.CommandText = "CREATE TRIGGER req006_fallo BEFORE INSERT ON Paneles FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='req006_controlado'";
            await trigger.ExecuteNonQueryAsync();
            try
            {
                try
                {
                    await servicio.CrearAsync(nodo.ID, Datos("Fallo"));
                    Exigir(false, "fallo-tecnico-no-provocado");
                }
                catch (DbUpdateException) { Exigir(true, "fallo-tecnico-propagado-no-duplicado"); }
                Exigir(!contexto.ChangeTracker.Entries().Any() && await contexto.Paneles.CountAsync() == 6, "fallo-sin-parciales-contexto-limpio");
            }
            finally { trigger.CommandText = "DROP TRIGGER req006_fallo"; await trigger.ExecuteNonQueryAsync(); }
        }
        Exigir((await servicio.CrearAsync(nodo.ID, Datos("Recuperado"))).EsExitoso, "recuperacion-mismo-contexto");
        await using (var lectura = Contexto(conexion))
        {
            var desaparecido = (await new NodoRepository(lectura).ObtenerPorIdAsync(ajeno.ID))!;
            var panel = desaparecido.AgregarPanel("Posterior", "M", 1, DateTime.UtcNow);
            await using (var eliminacion = Contexto(conexion))
                Exigir(await eliminacion.Nodos.Where(n => n.ID == ajeno.ID).ExecuteDeleteAsync() == 1, "nodo-eliminado-realmente");
            Exigir(!await lectura.Nodos.AnyAsync(n => n.ID == ajeno.ID), "nodo-ausente-antes-del-insert");
            var perdido = await new PanelRepository(lectura).GuardarAsync(panel);
            Exigir(perdido == ResultadoGuardadoPanel.NodoNoEncontrado
                && !lectura.ChangeTracker.Entries().Any(), "perdida-concurrente-nodo-fk-especifica");
        }
        await using (var lectura = Contexto(conexion))
            Exigir(await lectura.ParquesSolares.CountAsync() == 2 && await lectura.Nodos.CountAsync() == 2
                && await lectura.Inversores.CountAsync() == 2 && await lectura.Sensores.CountAsync() == 2
                && await lectura.Paneles.CountAsync() == 6, "grafos-previos-preservados-sin-huerfanos");
        using var cancelacion = new CancellationTokenSource(); cancelacion.Cancel();
        try { await servicio.CrearAsync(nodo.ID, Datos("Cancelado"), cancelacion.Token); Exigir(false, "cancelacion"); }
        catch (OperationCanceledException) { Exigir(true, "cancelacion-propagada"); }
        await File.WriteAllTextAsync(Carpeta + "/rutas.json", JsonSerializer.Serialize(new
        { parque = parque.ID, otroParque = otro.ID, nodo = nodo.ID, segundo = segundo.ID }));
    }

    private static Nodo AgregarNodo(ParqueSolar parque, string sufijo) => parque.AgregarNodo("Nodo " + sufijo, 0, 0,
        Inversor.Crear("Inversor " + sufijo, "I-" + sufijo, Sensor.Crear("Sensor " + sufijo, "S-" + sufijo)));

    private static async Task IniciarAplicacionRealAsync()
    {
        var puertos = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        Exigir(!puertos.Any(p => p.Port == 7178), "puerto-real-disponible-sin-detener-procesos-ajenos");
        var inicio = new System.Diagnostics.ProcessStartInfo("dotnet")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SolarUY") };
        inicio.ArgumentList.Add(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY/bin/Debug/net10.0/SolarUY.dll"));
        inicio.ArgumentList.Add("--urls=https://localhost:7178");
        inicio.ArgumentList.Add("--environment=Development");
        inicio.Environment["ConnectionStrings__SolarUy"] = ConexionAislada();
        inicio.Environment["Logging__LogLevel__Default"] = "None";
        using var proceso = System.Diagnostics.Process.Start(inicio)!;
        var salida = proceso.StandardOutput.ReadToEndAsync();
        var error = proceso.StandardError.ReadToEndAsync();
        await File.WriteAllTextAsync(Carpeta + "/real.pid", proceso.Id.ToString());
        Console.WriteLine("Aplicación real iniciada en https://localhost:7178 con el destino req006 aislado. Autenticación OIDC sin modificaciones.");
        await proceso.WaitForExitAsync();
        await Task.WhenAll(salida, error);
        Exigir(proceso.ExitCode == 0, "aplicacion-real-finalizada");
    }

    private static async Task VerificacionFinalAsync()
    {
        var conexion = ConexionAislada();
        await using var db = Contexto(conexion);
        var rutas = JsonDocument.Parse(await File.ReadAllTextAsync(Carpeta + "/rutas.json")).RootElement;
        var nodoId = rutas.GetProperty("nodo").GetGuid();
        await using (var sql = new MySqlConnection(conexion))
        {
            await sql.OpenAsync();
            await using var comando = sql.CreateCommand();
            comando.CommandText = "SELECT ENGINE FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Paneles'";
            Exigir((string?)await comando.ExecuteScalarAsync() == "InnoDB", "final-paneles-innodb");
            comando.CommandText = "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Paneles' AND COLUMN_NAME='ClaveNombre'";
            Exigir((string?)await comando.ExecuteScalarAsync() == "utf8mb4_0900_bin", "final-clave-colacion-binaria");
            comando.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='FK_Paneles_Nodos_IdNodo' AND DELETE_RULE='CASCADE'";
            Exigir(Convert.ToInt32(await comando.ExecuteScalarAsync()) == 1, "final-fk-fisica-cascada");
            comando.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Mediciones','Mantenimientos')";
            Exigir(Convert.ToInt32(await comando.ExecuteScalarAsync()) == 0, "final-sin-tablas-historicos");
        }
        var servicio = Servicio(db);
        var sufijo = Guid.NewGuid().ToString("N")[..6];
        Exigir((await servicio.CrearAsync(nodoId, Datos("Final Á " + sufijo))).EsExitoso
            && (await servicio.CrearAsync(nodoId, Datos("Final A " + sufijo))).EsExitoso, "final-acento-distingue-nombres-en-mysql");
        Exigir((await servicio.CrearAsync(nodoId, Datos(string.Concat(Enumerable.Repeat("😀", 100))))).EsExitoso,
            "final-nombre-100-unicode-suplementarios-persistidos");
        var antes = await db.Paneles.CountAsync();
        await using (var fallo = Contexto(conexion, new FalloPosteriorAlInsertInterceptor()))
        {
            try { await Servicio(fallo).CrearAsync(nodoId, Datos("Rollback " + sufijo)); Exigir(false, "rollback-no-provocado"); }
            catch (FalloControladoException) { Exigir(true, "final-fallo-despues-insert-antes-commit"); }
            Exigir(!fallo.ChangeTracker.Entries().Any(), "final-seguimiento-limpio-tras-rollback");
        }
        Exigir(await db.Paneles.CountAsync() == antes, "final-rollback-real-sin-insert-parcial");
        await File.WriteAllLinesAsync(Carpeta + "/final-resultados.txt", Resultados);
    }

    private static async Task ConcurrenciaAsync(string conexion, Guid nodoId, string nombreUno, string nombreDos, bool duplicado)
    {
        await using var uno = Contexto(conexion); await using var dos = Contexto(conexion);
        var nodoUno = (await new NodoRepository(uno).ObtenerPorIdAsync(nodoId))!;
        var nodoDos = (await new NodoRepository(dos).ObtenerPorIdAsync(nodoId))!;
        var resultados = await Task.WhenAll(new PanelRepository(uno).GuardarAsync(nodoUno.AgregarPanel(nombreUno, "M", 550.5f, DateTime.UtcNow)),
            new PanelRepository(dos).GuardarAsync(nodoDos.AgregarPanel(nombreDos, "M", 550.5f, DateTime.UtcNow)));
        Exigir(resultados.Count(r => r == ResultadoGuardadoPanel.Guardado) == (duplicado ? 1 : 2)
            && resultados.Count(r => r == ResultadoGuardadoPanel.ConflictoNombre) == (duplicado ? 1 : 0),
            duplicado ? "concurrencia-duplicado-un-solo-exito" : "concurrencia-distintos-ambos-preservados");
    }

    private sealed class RelojFijo : TimeProvider
    {
        internal static readonly DateTimeOffset Instante = new DateTimeOffset(2026, 10, 29, 14, 15, 16, TimeSpan.Zero).AddTicks(1_234_567);
        public override DateTimeOffset GetUtcNow() => Instante;
    }

    private sealed class CriterioFallidoException(string criterio) : Exception(criterio);
    private sealed class FalloControladoException : Exception;
    private sealed class FalloPosteriorAlInsertInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new FalloControladoException();
    }
    private sealed class UsuarioAuxiliar(string? rol) : IUsuarioActualService
    {
        public bool EstaAutenticado => rol is not null;
        public bool PerteneceAlRol(string nombre) => rol == nombre;
    }
    private sealed class RepositorioProhibido : INodoRepository, IPanelRepository
    {
        public Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> ExisteNumeroSerieInversorAsync(string numeroSerie, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> ExisteNumeroSerieSensorAsync(string numeroSerie, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ResultadoGuardadoNodo> GuardarAsync(Nodo nodo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ResultadoGuardadoPanel> GuardarAsync(Panel panel, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
