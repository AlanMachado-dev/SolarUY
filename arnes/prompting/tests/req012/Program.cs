using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Monitoreo;

try
{
    var conexion = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SOLARUY_REQ012_TEST_MYSQL") ?? "");
    Exigir(conexion.Server == "127.0.0.1" && conexion.Port == 39312 && conexion.Database == "solaruy_req012_validacion",
        "La validación requiere el MySQL aislado de req012.");
    var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
    PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
    if (args is ["oidc"])
    {
        await ServidorPrueba.EjecutarAsync(conexion.ConnectionString, 9449, oidc: true);
        return 0;
    }
    if (args is ["web", var puerto] && int.TryParse(puerto, out var numero) && numero is 39313 or 39314)
    {
        await ServidorPrueba.EjecutarAsync(conexion.ConnectionString, numero);
        return 0;
    }
    await using var contexto = new SolarUyDbContext(opciones.Options);
    if (args is ["reinicio", var paso] && paso is "programar" or "verificar")
    {
        await VerificarReinicioAsync(contexto, opciones.Options, paso);
        return 0;
    }
    Exigir(!await contexto.Database.CanConnectAsync(), "El esquema de validación debe ser nuevo.");

    var reloj = new RelojPrueba();
    var ahora = reloj.GetUtcNow().UtcDateTime;
    await contexto.Database.MigrateAsync();
    var parque = ParqueSolar.Crear("Parque req012", "Sur");
    var nodo = parque.AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "req012-I", Sensor.Crear("Sensor", "req012-S")));
    var panel = nodo.AgregarPanel("Panel", "Modelo", 500, ahora.AddDays(-1));
    var otro = nodo.AgregarPanel("Panel sin sesiones", "Modelo", 500, ahora.AddDays(-1));
    nodo.AgregarPanel("Panel navegador", "Modelo", 500, ahora.AddDays(-1));
    nodo.AgregarPanel("Panel reinicio", "Modelo", 500, ahora.AddDays(-1));
    var lecturaPrevia = Medicion.Crear(panel, ahora.AddSeconds(-1), 120, EstadoPanel.Operativo);
    contexto.Add(parque);
    contexto.Add(lecturaPrevia);
    await contexto.SaveChangesAsync();
    var anterior = contexto.Database.GetMigrations().Single(migracion => migracion.EndsWith("_IncorporarMedicion"));
    await contexto.GetService<IMigrator>().MigrateAsync(anterior);
    await contexto.Database.MigrateAsync();
    contexto.ChangeTracker.Clear();

    var fabrica = new FabricaContexto(opciones.Options);
    var notificacion = new NotificacionMedicion();
    var repositorio = new ControlPanelRepository(fabrica, reloj);
    var control = new ControlPanelService(repositorio, notificacion);
    var monitoreo = new MonitoreoService(new MedicionRepository(fabrica, reloj), notificacion);
    var admin = Usuario("Administrador");
    var operador = Usuario("Operador");
    var lecturas = new List<LecturaPanelDto>();
    using var suscripcion = notificacion.Suscribir(admin, panel.ID, lectura =>
    {
        lecturas.Add(lectura);
        return Task.CompletedTask;
    });

    Exigir(await monitoreo.ConsultarAsync(panel.ID) is { RevisionControl: 0, GeneracionActualW: 120 } historica &&
        historica.MedicionId == lecturaPrevia.ID, "Migración conserva el historial y agrega revisión inicial 0.");
    var fecha = ahora.AddSeconds(8);
    var orden = new ProgramarMantenimientoDto("Limpieza", fecha, "Técnico");
    Exigir((await control.EjecutarAsync(new ClaimsPrincipal(), panel.ID, orden)).Codigo == CodigoControlPanel.AccesoDenegado &&
        (await control.EjecutarAsync(operador, panel.ID, new DesactivarPanel())).Codigo == CodigoControlPanel.AccesoDenegado,
        "Autenticación y permisos en servidor con identidades de prueba.");
    var programada = await control.EjecutarAsync(operador, panel.ID, orden);
    Exigir(programada is { Codigo: CodigoControlPanel.Actualizado, Lectura: { RevisionControl: 1,
        EstadoControl: EstadoPanel.Operativo, Mantenimiento: { Estado: EstadoMantenimiento.Pendiente } }} &&
        lecturas.Single().MedicionId == lecturaPrevia.ID, "Programación confirmada y notificada sin crear medición.");
    var mantenimiento = programada.Lectura!.Mantenimiento!;
    Exigir(mantenimiento.Id != Guid.Empty && mantenimiento.FechaProgramadaUtc == fecha &&
        (await control.EjecutarAsync(admin, panel.ID, orden)).Codigo == CodigoControlPanel.DatosInvalidos,
        "ID generado en el servidor y segunda programación rechazada.");

    reloj.Avanzar(9);
    var codigo = await monitoreo.RegistrarAsync(new(panel.ID, 300, 1));
    Exigir(codigo == ResultadoRegistroMedicion.EnPausa &&
        await monitoreo.ConsultarAsync(panel.ID) is { RevisionControl: 2, EstadoControl: EstadoPanel.EnMantenimiento,
            GeneracionActualW: 0 } iniciada && iniciada.FechaUtc == fecha, "La muestra rechazada confirma el corte en la fecha programada.");
    Exigir((await repositorio.IniciarVencidoAsync(panel.ID, default)).Codigo == CodigoControlPanel.SinCambios,
        "El reintento de vencimiento conserva la revisión.");

    await Orden(new SimularFallaPanel(), EstadoPanel.EnFalla);
    await Orden(new DesactivarPanel(), EstadoPanel.Desactivado);
    Exigir((await control.EjecutarAsync(admin, panel.ID, new SimularArregloPanel())).Codigo == CodigoControlPanel.EstadoNoPermitido,
        "El arreglo desactualizado no puede reactivar un panel desactivado.");
    await Orden(new ActivarPanel(), EstadoPanel.EnMantenimiento);
    await Orden(new DesactivarPanel(), EstadoPanel.Desactivado);
    await Orden(new SimularFallaPanel(), EstadoPanel.EnFalla);
    await Orden(new SimularArregloPanel(), EstadoPanel.EnMantenimiento);
    reloj.Avanzar(90);
    await Orden(new FinalizarMantenimientoDto(mantenimiento.Id, "Realizado"), EstadoPanel.Operativo, operador);
    var detalle = await new NodoRepository(contexto).ObtenerPorIdAsync(nodo.ID);
    Exigir(detalle!.Paneles.Single(p => p.ID == panel.ID).UltimoMantenimiento == reloj.GetUtcNow().UtcDateTime,
        "Detalle del nodo proyecta la fecha de cierre UTC.");
    Exigir(await monitoreo.RegistrarAsync(new(panel.ID, 300, 1)) == ResultadoRegistroMedicion.RevisionDesactualizada,
        "Muestra anterior a la pausa rechazada tras reactivar.");
    var reanudada = (await monitoreo.ConsultarTransmisionAsync(panel.ID))!;
    Exigir(await monitoreo.RegistrarAsync(new(panel.ID, 301, reanudada.RevisionControl)) == ResultadoRegistroMedicion.Guardada,
        "Lectura con revisión vigente aceptada tras reactivar.");

    var nuevaFecha = reloj.GetUtcNow().UtcDateTime.AddSeconds(3);
    var nueva = await control.EjecutarAsync(admin, otro.ID, orden with { FechaProgramadaUtc = nuevaFecha });
    Exigir(nueva.Codigo == CodigoControlPanel.Actualizado, "Programación sin suscripciones.");
    reloj.Avanzar(4);
    var segundaInstancia = new ControlPanelRepository(fabrica, reloj);
    var segundoMonitoreo = new MonitoreoService(new MedicionRepository(fabrica, reloj), new NotificacionMedicion());
    var pruebaSimultanea = Task.WhenAll(
        new VencimientosMantenimientoService(repositorio, notificacion, reloj).ProcesarAsync(default),
        new VencimientosMantenimientoService(segundaInstancia, new NotificacionMedicion(), reloj).ProcesarAsync(default),
        new ControlPanelService(segundaInstancia, new NotificacionMedicion()).EjecutarAsync(admin, otro.ID, new ActivarPanel()),
        segundoMonitoreo.RegistrarAsync(new(otro.ID, 400, 1)));
    await pruebaSimultanea;
    Exigir(await monitoreo.ConsultarAsync(otro.ID) is { RevisionControl: 2,
        EstadoControl: EstadoPanel.EnMantenimiento, GeneracionActualW: 0 },
        "Dos procesadores y una muestra simultánea dejan un único inicio, sin sesión de monitoreo.");

    var estable = await monitoreo.ConsultarAsync(panel.ID);
    await contexto.Database.ExecuteSqlRawAsync("CREATE TRIGGER req012_fallo BEFORE INSERT ON Medicion FOR EACH ROW " +
        "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req012'");
    try
    {
        var inmediato = orden with { FechaProgramadaUtc = reloj.GetUtcNow().UtcDateTime };
        var resultado = await control.EjecutarAsync(admin, panel.ID, inmediato);
        Exigir(resultado.Codigo == CodigoControlPanel.ErrorDePersistencia && estable == await monitoreo.ConsultarAsync(panel.ID),
            "El fallo real de inserción revierte el mantenimiento, el estado y la revisión.");
    }
    finally
    {
        await contexto.Database.ExecuteSqlRawAsync("DROP TRIGGER req012_fallo");
    }

    var historial = await contexto.Set<Medicion>()
        .Where(medicion => EF.Property<Guid>(medicion, "IdPanel") == panel.ID).OrderBy(medicion => medicion.FechaUtc)
        .Select(medicion => new { medicion.FechaUtc, medicion.GeneracionActualW, medicion.Estado }).ToListAsync();
    Exigir(historial.First().GeneracionActualW == 120 && historial.Last().GeneracionActualW == 301 &&
        historial.Skip(1).SkipLast(1).All(medicion => medicion.GeneracionActualW == 0) &&
        historial.Zip(historial.Skip(1)).All(par => par.First.FechaUtc < par.Second.FechaUtc),
        "Historial ordenado con marcas a 0 W en todo el intervalo de pausa.");
    var pausa = EnergiaEstimadaPanel.Calcular(historial.Select(muestra =>
        new LecturaEnergia(Guid.NewGuid(), muestra.FechaUtc, (decimal)muestra.GeneracionActualW)), fecha, historial[^1].FechaUtc);
    Exigir(pausa.EnergiaKwh == 0, "La estimación de energía del intervalo de pausa es 0 kWh.");
    return 0;

    async Task Orden(OrdenControlPanel accion, EstadoPanel esperado, ClaimsPrincipal? usuario = null)
    {
        var resultado = await control.EjecutarAsync(usuario ?? admin, panel.ID, accion);
        Exigir(resultado.Codigo == CodigoControlPanel.Actualizado && resultado.Lectura?.EstadoControl == esperado,
            $"{accion.GetType().Name}: {esperado}.");
    }
}
catch (Exception error)
{
    Console.Error.WriteLine($"Verificación interrumpida ({error.GetBaseException().GetType().Name}).");
    return 1;
}

static ClaimsPrincipal Usuario(string rol) => new(new ClaimsIdentity([new(ClaimTypes.Role, rol)], "Req012"));

static async Task VerificarReinicioAsync(SolarUyDbContext contexto, DbContextOptions<SolarUyDbContext> opciones, string paso)
{
    var id = await contexto.Paneles.Where(panel => panel.Nombre.Valor == "Panel reinicio")
        .Select(panel => panel.ID).SingleAsync();
    var reloj = TimeProvider.System;
    var notificacion = new NotificacionMedicion();
    var fabrica = new FabricaContexto(opciones);
    if (paso == "programar")
    {
        var control = new ControlPanelService(new ControlPanelRepository(fabrica, reloj), notificacion);
        var fecha = reloj.GetUtcNow().UtcDateTime.AddSeconds(8);
        var resultado = await control.EjecutarAsync(Usuario("Administrador"), id,
            new ProgramarMantenimientoDto("Inspección", fecha, "Técnico"));
        Exigir(resultado.Lectura is { RevisionControl: 1, Mantenimiento.Estado: EstadoMantenimiento.Pendiente },
            "Mantenimiento pendiente en panel sin sesiones abiertas antes del reinicio.");
        return;
    }

    var monitoreo = new MonitoreoService(new MedicionRepository(fabrica, reloj), notificacion);
    var lectura = await monitoreo.ConsultarAsync(id);
    var cantidad = await contexto.Set<Medicion>().CountAsync(medicion => EF.Property<Guid>(medicion, "IdPanel") == id);
    Exigir(lectura is { RevisionControl: 2, EstadoControl: EstadoPanel.EnMantenimiento, GeneracionActualW: 0,
        Mantenimiento.Estado: EstadoMantenimiento.EnCurso } && cantidad == 1,
        "Los dos servidores reanudados persistieron un único inicio vencido sin abrir el monitoreo.");
}

static void Exigir(bool condicion, string descripcion)
{
    Console.WriteLine($"{(condicion ? "OK" : "FALLO")} {descripcion}");
    if (!condicion)
        throw new InvalidOperationException();
}

sealed class FabricaContexto(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
}

sealed class RelojPrueba : TimeProvider
{
    private DateTimeOffset _ahora = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _ahora;
    public void Avanzar(int segundos) => _ahora = _ahora.AddSeconds(segundos);
}
