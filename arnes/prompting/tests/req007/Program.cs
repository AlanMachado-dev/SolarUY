using System.Security.Claims;
using SolarUY.Application.Monitoreo;
using SolarUY.Presentation.Monitoreo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Domain.Paneles;
using SolarUY.Infrastructure.Persistencia;

try
{
    var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("SolarUY"))
        .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
        .AddEnvironmentVariables().Build();
    var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(config));
    var destino = $"solaruy_req007_{Guid.NewGuid():N}";
    if (destino.Equals(conexion.Database, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException();
    conexion.Database = destino;
    var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
    PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
    await using var contexto = new SolarUyDbContext(opciones.Options);
    var migraciones = contexto.Database.GetMigrations().ToArray();
    await contexto.GetService<IMigrator>().MigrateAsync(migraciones[^2]);
    var parque = ParqueSolar.Crear("Parque req007", "Sur");
    var nodo = parque.AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")));
    var panel = nodo.AgregarPanel("Panel", "Modelo", 500, DateTime.UtcNow);
    contexto.Add(parque);
    await contexto.SaveChangesAsync();
    await contexto.Database.MigrateAsync();
    var repositorio = new MedicionRepository(new FabricaContexto(opciones.Options));
    var panelPersistido = await repositorio.ObtenerPanelAsync(panel.ID, default);
    if (panelPersistido is null || await repositorio.ObtenerUltimaAsync(panel.ID, default) is not null)
        throw new InvalidOperationException();
    var medicion = Medicion.Crear(panelPersistido, DateTime.UtcNow, 42, EstadoPanel.Operativo);
    if (!await repositorio.GuardarAsync(medicion, default))
        throw new InvalidOperationException();
    var guardada = await repositorio.ObtenerUltimaAsync(panel.ID, default);
    if (guardada?.ID != medicion.ID || guardada.FechaUtc != medicion.FechaUtc || guardada.FechaUtc.Kind != DateTimeKind.Utc)
        throw new InvalidOperationException();
    var notificacion = new NotificacionMedicion();
    var servicio = new MonitoreoService(repositorio, notificacion);
    var lecturasAdmin = new List<LecturaPanelDto>();
    var lecturasOperador = new List<LecturaPanelDto>();
    var lecturasOtroPanel = new List<LecturaPanelDto>();
    using var admin = Suscribir("Administrador", panel.ID, lecturasAdmin);
    using var operador = Suscribir("Operador", panel.ID, lecturasOperador);
    using var otro = Suscribir("Operador", Guid.NewGuid(), lecturasOtroPanel);

    var entrada = new RegistrarMedicionDto(panel.ID, 80, EstadoPanel.Operativo);
    if (await servicio.RegistrarAsync(entrada) != ResultadoRegistroMedicion.Guardada)
        throw new InvalidOperationException();
    var confirmada = await servicio.ConsultarAsync(panel.ID);
    if (confirmada is null || lecturasAdmin.Single() != confirmada ||
        lecturasOperador.Single() != confirmada || lecturasOtroPanel.Count != 0)
        throw new InvalidOperationException();
    if ((await servicio.ConsultarAsync(Guid.NewGuid())) is not null)
        throw new InvalidOperationException();

    await contexto.Database.ExecuteSqlRawAsync(
        "CREATE TRIGGER req007_fallo BEFORE INSERT ON Medicion FOR EACH ROW " +
        "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req007'");
    try
    {
        if (await servicio.RegistrarAsync(entrada) != ResultadoRegistroMedicion.ErrorDePersistencia ||
            lecturasAdmin.Count != 1 || lecturasOperador.Count != 1 ||
            await servicio.ConsultarAsync(panel.ID) != confirmada)
            throw new InvalidOperationException();
    }
    finally
    {
        await contexto.Database.ExecuteSqlRawAsync("DROP TRIGGER req007_fallo");
    }

    IDisposable Suscribir(string rol, Guid panelId, List<LecturaPanelDto> lecturas) =>
        notificacion.Suscribir(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, rol)], "IntegracionAuxiliar")), panelId, lectura =>
            {
                lecturas.Add(lectura);
                return Task.CompletedTask;
            });

    var evidencia = $"OK {destino}: panel previo conservado con historial vacío y primera medición recuperada en UTC, entrega por panel y fallo real de escritura sin nueva entrega. Integración auxiliar con roles de prueba.";
    await File.WriteAllTextAsync("artifacts/req007/migracion.txt", evidencia);
    Console.WriteLine(evidencia);
    return 0;
}
catch (Exception error)
{
    Console.WriteLine($"Fallo de verificación: {error.GetBaseException().GetType().Name}.");
    return 1;
}

sealed class FabricaContexto(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
}
