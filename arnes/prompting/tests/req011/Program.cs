using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SolarUY.Application.Monitoreo;
using SolarUY.Client.Monitoreo;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;

try
{
    var conexion = new MySqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("SOLARUY_REQ011_TEST_MYSQL") ?? "");
    Exigir(conexion.Server is "127.0.0.1" or "localhost" &&
        conexion.Database.StartsWith("solaruy_req011_", StringComparison.Ordinal),
        "Indicá un destino local y dedicado a req011.");

    var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
    PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
    await using var contexto = new SolarUyDbContext(opciones.Options);
    Exigir(!await contexto.Database.CanConnectAsync(), "El esquema de prueba debe ser nuevo.");
    await contexto.Database.MigrateAsync();

    var parque = ParqueSolar.Crear("Parque req011", "Sur");
    var nodo = parque.AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")));
    var panel = nodo.AgregarPanel("Panel", "Modelo", 500, DateTime.UtcNow);
    var otro = nodo.AgregarPanel("Otro", "Modelo", 500, DateTime.UtcNow);
    contexto.Add(parque);
    await contexto.SaveChangesAsync();

    var fabrica = new FabricaContexto(opciones.Options);
    var notificacion = new NotificacionPrueba();
    var control = new ControlPanelService(new ControlPanelRepository(fabrica, TimeProvider.System), notificacion);
    var monitoreo = new MonitoreoService(new MedicionRepository(fabrica, TimeProvider.System), notificacion);
    var admin = Usuario("Administrador");
    Exigir((await control.EjecutarAsync(Usuario("Operador"), panel.ID, new DesactivarPanel())).Codigo ==
        CodigoControlPanel.AccesoDenegado, "Operador no puede desactivar.");
    Exigir((await control.EjecutarAsync(new ClaimsPrincipal(), panel.ID, new DesactivarPanel())).Codigo ==
        CodigoControlPanel.AccesoDenegado, "Una sesión anónima no puede desactivar.");
    Exigir((await control.EjecutarAsync(admin, Guid.NewGuid(), new DesactivarPanel())).Codigo ==
        CodigoControlPanel.PanelNoEncontrado, "Identificador inexistente.");

    Exigir(await monitoreo.RegistrarAsync(new(panel.ID, 120, 0)) ==
        ResultadoRegistroMedicion.Guardada, "Lectura inicial.");
    var ordenes = await Task.WhenAll(control.EjecutarAsync(admin, panel.ID, new DesactivarPanel()),
        control.EjecutarAsync(admin, panel.ID, new DesactivarPanel()));
    Exigir(ordenes.Count(resultado => resultado.Codigo == CodigoControlPanel.Actualizado) == 1 &&
        ordenes.Count(resultado => resultado.Codigo == CodigoControlPanel.SinCambios) == 1 &&
        notificacion.Lecturas.Count == 2, "Una sola transición y una notificación para órdenes concurrentes.");
    Exigir(await monitoreo.ConsultarAsync(panel.ID) is
        { EstadoControl: EstadoPanel.Desactivado, Estado: EstadoPanel.Desactivado, GeneracionActualW: 0 },
        "Estado persistido y cero confirmado al desactivar.");
    Exigir(await monitoreo.ConsultarTransmisionAsync(panel.ID) is { Habilitado: false } &&
        await monitoreo.ConsultarTransmisionAsync(otro.ID) is { Habilitado: true }, "Aislamiento por panel.");
    Exigir(await monitoreo.RegistrarAsync(new(panel.ID, 120, 0)) !=
        ResultadoRegistroMedicion.Guardada, "Rechazo de lectura anterior a la desactivación.");

    var simulador = new InversorSimulado();
    var activacion = await control.EjecutarAsync(admin, panel.ID, new ActivarPanel());
    Exigir(activacion is { Codigo: CodigoControlPanel.Actualizado, Lectura.EstadoControl: EstadoPanel.Operativo } &&
        await monitoreo.ConsultarTransmisionAsync(panel.ID) is { Habilitado: true }, "Activación persistida.");
    Exigir(await monitoreo.RegistrarAsync(new(panel.ID, 0, 1)) !=
        ResultadoRegistroMedicion.Guardada, "Rechazo de lectura anterior a la activación.");
    for (var i = 0; i < 100; i++)
        Exigir(simulador.LeerPotencia(500) is >= 0 and < 500, "Potencia operativa dentro del rango.");

    await contexto.Database.ExecuteSqlRawAsync(
        "CREATE TRIGGER req011_fallo BEFORE INSERT ON Medicion FOR EACH ROW " +
        "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req011'");
    try
    {
        var cantidad = notificacion.Lecturas.Count;
        Exigir((await control.EjecutarAsync(admin, panel.ID, new DesactivarPanel())).Codigo ==
            CodigoControlPanel.ErrorDePersistencia, "Fallo controlado al registrar el cambio.");
        Exigir(await monitoreo.ConsultarAsync(panel.ID) == activacion.Lectura &&
            notificacion.Lecturas.Count == cantidad, "Reversión atómica sin notificar el cambio fallido.");
    }
    finally
    {
        await contexto.Database.ExecuteSqlRawAsync("DROP TRIGGER req011_fallo");
    }

    Console.WriteLine("MySQL: roles, activación, desactivación, concurrencia, simulador y reversión correctos.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Verificación fallida ({error.GetBaseException().GetType().Name}).");
    return 1;
}

static ClaimsPrincipal Usuario(string rol) =>
    new(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "Prueba"));

static void Exigir(bool condicion, string descripcion)
{
    if (!condicion)
    {
        Console.Error.WriteLine(descripcion);
        throw new InvalidOperationException();
    }
}

sealed class FabricaContexto(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
}

sealed class NotificacionPrueba : INotificacionMedicion
{
    public ConcurrentQueue<LecturaPanelDto> Lecturas { get; } = new();

    public Task PublicarAsync(LecturaPanelDto lectura)
    {
        Lecturas.Enqueue(lectura);
        return Task.CompletedTask;
    }
}
