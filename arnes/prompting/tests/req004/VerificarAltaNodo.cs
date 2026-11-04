// Integración auxiliar con MySQL real. Las identidades locales no acreditan un ingreso OIDC.
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;

internal static class VerificarAltaNodo
{
    private static int _comprobaciones;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("oidc"))
            {
                await VerificarOidc.ServirAsync();
                return 0;
            }
            if (args.Contains("ssr"))
            {
                await VerificarSsr.EjecutarAsync(args.Contains("preview"));
                return 0;
            }
            else
                await EjecutarAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { correcto = true, comprobaciones = _comprobaciones,
                alcance = "Integración MySQL y servicios; identidades locales, sin aceptación OIDC" }));
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { correcto = false,
                tipo = error.GetBaseException().GetType().Name,
                origen = error.TargetSite?.DeclaringType?.Name + "." + error.TargetSite?.Name,
                codigo = error.GetBaseException() is MySqlException mysql ? mysql.Number : 0,
                comprobaciones = _comprobaciones }));
            return 1;
        }
    }

    private static async Task EjecutarAsync()
    {
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var opcionesConexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var baseHabitual = opcionesConexion.Database;
        var baseAislada = "solaruy_req004_" + Guid.NewGuid().ToString("N")[..12];
        Exigir(baseHabitual != baseAislada, "destino-aislado");
        opcionesConexion.Database = "";
        await using (var admin = new MySqlConnection(opcionesConexion.ConnectionString))
        {
            await admin.OpenAsync();
            await using var comando = admin.CreateCommand();
            comando.CommandText = $"CREATE DATABASE `{baseAislada}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
            await comando.ExecuteNonQueryAsync();
        }
        opcionesConexion.Database = baseAislada;
        var conexion = opcionesConexion.ConnectionString;
        await File.WriteAllTextAsync("artifacts/req004/destino-aislado.json", JsonSerializer.Serialize(new { Base = baseAislada }));
        SolarUyDbContext Contexto(FalloAntesDeConfirmar? fallo = null)
        {
            var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
            PersistenciaConfiguration.ConfigurarContexto(opciones, conexion);
            if (fallo is not null) opciones.AddInterceptors(fallo);
            return new SolarUyDbContext(opciones.Options);
        }

        await using var contexto = Contexto();
        await contexto.GetService<IMigrator>().MigrateAsync("20261019182401_InicialMySql");
        var parque = ParqueSolar.Crear("Parque req004", "Destino aislado");
        var otroParque = ParqueSolar.Crear("Otro parque req004", "Destino aislado");
        var parques = new ParqueSolarRepository(contexto);
        await parques.GuardarAsync(parque);
        await parques.GuardarAsync(otroParque);
        contexto.ChangeTracker.Clear();
        await contexto.Database.MigrateAsync();
        var conservado = await parques.ObtenerPorIdAsync(parque.ID);
        Exigir(conservado?.Nombre.Valor == parque.Nombre.Valor && conservado.Ubicacion == parque.Ubicacion
            && conservado.Nodos.Count == 0, "migracion-conserva-parques");
        Exigir((await parques.ListarAsync()).Count == 2, "migracion-conserva-todos-los-parques");

        var acceso = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var usuario = new UsuarioActualService(acceso);
        var nodos = new NodoRepository(contexto);
        var servicio = new NodoService(nodos, parques, usuario, new MedicionRepositorySinLecturas());
        var prohibido = new AccesoProhibidoRepository();
        var servicioProhibido = new NodoService(prohibido, prohibido, usuario, new MedicionRepositorySinLecturas());
        foreach (var rol in new string?[] { null, "Operador", "", "administrador" })
        {
            Identificar(acceso, rol);
            var rechazo = await servicioProhibido.CrearAsync(parque.ID, Datos());
            Exigir(!rechazo.EsExitoso && rechazo.Nodo is null && rechazo.Errores.Single().Codigo ==
                (rol is null ? CodigoErrorAltaNodo.AutenticacionRequerida : CodigoErrorAltaNodo.AccesoDenegado), "autorizacion-antes-de-repositorios");
        }
        Identificar(acceso, "Administrador");
        foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
            Exigir((await servicio.CrearAsync(id, Datos())).Errores.Single().Codigo == CodigoErrorAltaNodo.ParqueNoEncontrado, "parque-inexistente");

        foreach (var propiedad in typeof(CrearNodoDto).GetProperties().Where(propiedad => propiedad.Name != "EndpointSensor"))
        {
            foreach (var valor in propiedad.PropertyType == typeof(string) ? new object?[] { null, "", "   " } : new object?[] { null })
            {
                var datos = Datos();
                propiedad.SetValue(datos, valor);
                var rechazo = await servicio.CrearAsync(parque.ID, datos);
                Exigir(!rechazo.EsExitoso && rechazo.Errores.Single().Codigo == CodigoErrorAltaNodo.DatosInvalidos
                    && rechazo.Errores.Single().Campo == propiedad.Name, "campo-obligatorio-" + propiedad.Name);
            }
        }
        foreach (var valor in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -181f, 181f })
        {
            var datos = Datos(); datos.Longitud = valor;
            Exigir((await servicio.CrearAsync(parque.ID, datos)).Errores.Single().Campo == "Longitud", "longitud-invalida");
            datos = Datos(); datos.Latitud = valor;
            Exigir((await servicio.CrearAsync(parque.ID, datos)).Errores.Single().Campo == "Latitud", "latitud-invalida");
        }
        Exigir(await contexto.Nodos.CountAsync() == 0 && await contexto.Inversores.CountAsync() == 0
            && await contexto.Sensores.CountAsync() == 0, "rechazos-sin-escrituras");

        var primero = (await servicio.CrearAsync(parque.ID, Datos())).Nodo!;
        Exigir(primero is not null && primero.CantidadPaneles == 0 && primero.ParqueId == parque.ID
            && primero.Latitud == 0 && primero.Longitud == 0 && primero.Inversor.Sensor.Endpoint is null, "alta-completa-cero-endpoint-opcional");
        foreach (var coordenadas in new[] { (-90f, -180f), (90f, 180f) })
        {
            var datos = Datos(); datos.Latitud = coordenadas.Item1; datos.Longitud = coordenadas.Item2;
            datos.EndpointSensor = "  texto sin protocolo  ";
            var alta = await servicio.CrearAsync(parque.ID, datos);
            Exigir(alta.EsExitoso && alta.Nodo!.Inversor.Sensor.Endpoint == "texto sin protocolo", "limites-endpoint-y-series-repetidas");
        }
        await using (var nuevo = Contexto())
        {
            var recargado = (await new ParqueSolarRepository(nuevo).ObtenerPorIdAsync(parque.ID))!;
            Exigir(recargado.Nodos.Count == 3 && recargado.NodosIds.Contains(primero!.ID)
                && recargado.CapacidadTotalEstimadaMw == 0m, "lectura-nueva-con-coleccion");
            var nodo = recargado.Nodos.Single(nodo => nodo.ID == primero!.ID);
            Exigir(nodo.Inversor.ID == primero!.Inversor.ID && nodo.Inversor.Sensor.ID == primero.Inversor.Sensor.ID
                && ReferenceEquals(nodo.Inversor.Nodo, nodo) && ReferenceEquals(nodo.Inversor.Sensor.Inversor, nodo.Inversor), "identidades-e-inversas-recargadas");
            var ids = recargado.Nodos.SelectMany(nodo => new[] { nodo.ID, nodo.Inversor.ID, nodo.Inversor.Sensor.ID }).ToArray();
            Exigir(ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == 9, "ids-distintos-generados");
            Exigir((await new ParqueSolarRepository(nuevo).ObtenerPorIdAsync(otroParque.ID))!.Nodos.Count == 0, "otro-parque-sin-efectos");
            try { Inversor.Crear("i", "i", nodo.Inversor.Sensor); throw new Exception("sensor-compartido"); }
            catch (DatosNodoInvalidosException) { Exigir(true, "sensor-recargado-protegido"); }
            try { otroParque.AgregarNodo("n", 0, 0, nodo.Inversor); throw new Exception("inversor-compartido"); }
            catch (DatosNodoInvalidosException) { Exigir(true, "inversor-recargado-protegido"); }
            Exigir(!nuevo.ChangeTracker.Entries().Any(), "lectura-sin-seguimiento");
        }

        await using (var sql = new MySqlConnection(conexion))
        {
            await sql.OpenAsync();
            await RechazarSqlAsync(sql, "INSERT INTO Inversores (ID,Nombre,NumeroSerie,IdNodo) VALUES (UUID(),'i','i',@id)", primero!.ID, 1062);
            await RechazarSqlAsync(sql, "INSERT INTO Sensores (ID,Nombre,NumeroSerie,IdInversor) VALUES (UUID(),'s','s',@id)", primero.Inversor.ID, 1062);
            await RechazarSqlAsync(sql, "INSERT INTO Nodos (ID,Nombre,Latitud,Longitud,IdParque) VALUES (UUID(),'n',0,0,@id)", Guid.NewGuid(), 1452);
            await RechazarSqlAsync(sql, "INSERT INTO Inversores (ID,Nombre,NumeroSerie,IdNodo) VALUES (UUID(),'i','i',@id)", Guid.NewGuid(), 1452);
            await RechazarSqlAsync(sql, "INSERT INTO Sensores (ID,Nombre,NumeroSerie,IdInversor) VALUES (UUID(),'s','s',@id)", Guid.NewGuid(), 1452);
        }

        var fallo = new FalloAntesDeConfirmar { Activo = true };
        await using (var conFallo = Contexto(fallo))
        {
            var servicioConFallo = new NodoService(new NodoRepository(conFallo), new ParqueSolarRepository(conFallo), usuario, new MedicionRepositorySinLecturas());
            try { await servicioConFallo.CrearAsync(parque.ID, Datos()); throw new Exception("fallo-no-provocado"); }
            catch (FalloControladoException) { Exigir(true, "fallo-antes-del-commit"); }
            Exigir(!conFallo.ChangeTracker.Entries().Any(), "contexto-limpio-tras-fallo");
            await using var comprobacion = Contexto();
            Exigir(await comprobacion.Nodos.CountAsync() == 3 && await comprobacion.Inversores.CountAsync() == 3
                && await comprobacion.Sensores.CountAsync() == 3, "rollback-completo");
            fallo.Activo = false;
            Exigir((await servicioConFallo.CrearAsync(parque.ID, Datos())).EsExitoso, "recuperacion-mismo-contexto");
        }

        await using (var uno = Contexto())
        await using (var dos = Contexto())
        {
            var parqueUno = (await new ParqueSolarRepository(uno).ObtenerPorIdAsync(parque.ID))!;
            var parqueDos = (await new ParqueSolarRepository(dos).ObtenerPorIdAsync(parque.ID))!;
            var resultados = await Task.WhenAll(new NodoRepository(uno).GuardarAsync(NuevoNodo(parqueUno)),
                new NodoRepository(dos).GuardarAsync(NuevoNodo(parqueDos)));
            Exigir(resultados.All(resultado => resultado == ResultadoGuardadoNodo.Guardado), "dos-altas-concurrentes");
        }
        await using (var nuevo = Contexto())
        {
            Exigir((await new ParqueSolarRepository(nuevo).ObtenerPorIdAsync(parque.ID))!.Nodos.Count == 6
                && await nuevo.Nodos.CountAsync() == 6 && await nuevo.Inversores.CountAsync() == 6
                && await nuevo.Sensores.CountAsync() == 6, "concurrencia-preserva-grafos");
        }

        // Desaparición real después de leer y antes de guardar, exclusivamente en la base creada.
        await using (var lectura = Contexto())
        {
            var repositorio = new NodoConParqueEliminadoRepository(new NodoRepository(lectura), async () =>
            {
                await using var eliminacion = Contexto();
                await eliminacion.ParquesSolares.Where(p => p.ID == otroParque.ID).ExecuteDeleteAsync();
            });
            var resultado = await new NodoService(repositorio, new ParqueSolarRepository(lectura), usuario, new MedicionRepositorySinLecturas())
                .CrearAsync(otroParque.ID, Datos());
            Exigir(resultado.Errores.Single().Codigo == CodigoErrorAltaNodo.ParqueNoEncontrado && resultado.Nodo is null,
                "perdida-concurrente-traducida");
            Exigir(!lectura.ChangeTracker.Entries().Any(), "perdida-concurrente-limpia-contexto");
            Exigir(await lectura.Nodos.CountAsync() == 6 && await lectura.Inversores.CountAsync() == 6
                && await lectura.Sensores.CountAsync() == 6, "perdida-concurrente-sin-huerfanos");
        }
        using var cancelacion = new CancellationTokenSource(); cancelacion.Cancel();
        try { await servicio.CrearAsync(parque.ID, Datos(), cancelacion.Token); throw new Exception("cancelacion-no-propagada"); }
        catch (OperationCanceledException) { Exigir(true, "cancelacion-propagada"); }
    }

    private static CrearNodoDto Datos() => new()
    {
        Nombre = " Nodo ", Latitud = 0, Longitud = 0, NombreInversor = " Inversor ", NumeroSerieInversor = " I-1 ",
        NombreSensor = " Sensor ", NumeroSerieSensor = " S-1 ", EndpointSensor = "   "
    };

    private static Nodo NuevoNodo(ParqueSolar parque) => parque.AgregarNodo("Concurrente", 0, 0,
        Inversor.Crear("Inversor", "I-1", Sensor.Crear("Sensor", "S-1")));

    private static void Identificar(HttpContextAccessor acceso, string? rol)
    {
        acceso.HttpContext!.User = rol is null ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, rol) }, "Identidad-local-auxiliar"));
    }

    private static void Exigir(bool condicion, string criterio)
    {
        if (!condicion) throw new InvalidOperationException(criterio);
        _comprobaciones++;
        Console.WriteLine("OK " + criterio);
    }

    private static async Task RechazarSqlAsync(MySqlConnection conexion, string sql, Guid id, int codigo)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("@id", id.ToString("D"));
        try { await comando.ExecuteNonQueryAsync(); throw new Exception("restriccion-no-aplicada"); }
        catch (MySqlException error) when (error.Number == codigo) { Exigir(true, "restriccion-mysql-" + codigo); }
    }

    private sealed class FalloControladoException : Exception { }
    private sealed class FalloAntesDeConfirmar : SaveChangesInterceptor
    {
        public bool Activo { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => Activo ? throw new FalloControladoException() : ValueTask.FromResult(result);
    }

    private sealed class NodoConParqueEliminadoRepository(INodoRepository repositorio, Func<Task> eliminar) : INodoRepository
    {
        public async Task<ResultadoGuardadoNodo> GuardarAsync(Nodo nodo, CancellationToken cancellationToken = default)
        {
            await eliminar();
            return await repositorio.GuardarAsync(nodo, cancellationToken);
        }
    }

    private sealed class AccesoProhibidoRepository : INodoRepository, IParqueSolarRepository
    {
        public Task<ResultadoGuardadoNodo> GuardarAsync(Nodo nodo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ParqueSolar?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ParqueSolar>> ListarAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> ExisteNombreAsync(string claveNombre, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ResultadoGuardadoParqueSolar> GuardarAsync(ParqueSolar parque, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
