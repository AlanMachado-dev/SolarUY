using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MySql.Data.MySqlClient;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class ControlPanelRepository(
    IDbContextFactory<SolarUyDbContext> contextFactory, TimeProvider reloj) : IControlPanelRepository
{
    public Task<ResultadoControlPanel> EjecutarAsync(Guid panelId, OrdenControlPanel orden, CancellationToken cancellationToken) =>
        ProcesarAsync(panelId, orden, cancellationToken);

    public Task<ResultadoControlPanel> IniciarVencidoAsync(Guid panelId, CancellationToken cancellationToken) =>
        ProcesarAsync(panelId, null, cancellationToken);

    public async Task<IReadOnlyList<Guid>> BuscarVencidosAsync(DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await contexto.Set<Mantenimiento>().AsNoTracking()
            .Where(mantenimiento => mantenimiento.InicioProcesadoUtc == null && mantenimiento.FechaRealizada == null &&
                mantenimiento.FechaProgramada <= ahoraUtc)
            .OrderBy(mantenimiento => mantenimiento.FechaProgramada)
            .Select(mantenimiento => EF.Property<Guid>(mantenimiento, "IdPanel"))
            .ToListAsync(cancellationToken);
    }

    private async Task<ResultadoControlPanel> ProcesarAsync(Guid panelId, OrdenControlPanel? orden, CancellationToken cancelacion)
    {
        try
        {
            await using var contexto = await contextFactory.CreateDbContextAsync(cancelacion);
            await using var transaccion = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancelacion);
            var panel = await BloquearPanelAsync(contexto, panelId, cancelacion);
            if (panel is null)
                return new(CodigoControlPanel.PanelNoEncontrado);

            var ultima = await MedicionRepository.LeerUltimaAsync(contexto, panelId, cancelacion);
            if (orden is FinalizarMantenimientoDto cierre && panel.MantenimientoAbierto?.ID != cierre.MantenimientoId)
            {
                // Materializa el ID solicitado para reconocer el reintento de un cierre anterior.
                await contexto.Set<Mantenimiento>()
                    .Where(registro => registro.ID == cierre.MantenimientoId &&
                        EF.Property<Guid>(registro, "IdPanel") == panelId)
                    .LoadAsync(cancelacion);
            }

            var ahoraUtc = reloj.GetUtcNow().UtcDateTime;
            var revision = Revision(contexto, panel);
            var inicial = revision.CurrentValue;
            ultima = AplicarVencimiento(contexto, panel, ultima, ahoraUtc);
            var estadoPrevio = panel.Estado;
            var (codigo, error) = orden is null
                ? (revision.CurrentValue != inicial ? CodigoControlPanel.Actualizado : CodigoControlPanel.SinCambios, null)
                : AplicarOrden(panel, orden, ahoraUtc);
            if (orden is not null && codigo == CodigoControlPanel.Actualizado)
                ultima = RegistrarCambio(contexto, panel, estadoPrevio, ultima, ahoraUtc);

            var cambio = revision.CurrentValue != inicial;
            if (cambio)
                await contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
            return new(codigo, LecturaPanelDto.Desde(panel, ultima, revision.CurrentValue), cambio, error);
        }
        catch (Exception error) when (error is DbUpdateException or MySqlException)
        {
            return new(CodigoControlPanel.ErrorDePersistencia);
        }
    }

    private static (CodigoControlPanel Codigo, string? Error) AplicarOrden(Panel panel, OrdenControlPanel orden, DateTime ahoraUtc)
    {
        try
        {
            var cambio = orden switch
            {
                SimularFallaPanel => panel.SimularFalla(),
                SimularArregloPanel => panel.SimularArreglo(),
                DesactivarPanel => panel.Desactivar(),
                ActivarPanel => panel.Activar(),
                FinalizarMantenimientoDto cierre => panel.FinalizarMantenimiento(cierre.MantenimientoId, ahoraUtc, cierre.Observaciones),
                ProgramarMantenimientoDto programa => Programar(panel, programa, ahoraUtc),
                _ => ResultadoCambioEstadoPanel.NoPermitido
            };
            return (cambio switch
            {
                ResultadoCambioEstadoPanel.Actualizado => CodigoControlPanel.Actualizado,
                ResultadoCambioEstadoPanel.SinCambios => CodigoControlPanel.SinCambios,
                _ => CodigoControlPanel.EstadoNoPermitido
            }, null);
        }
        catch (DatosPanelInvalidosException error)
        {
            return (CodigoControlPanel.DatosInvalidos, error.Message);
        }
    }

    private static ResultadoCambioEstadoPanel Programar(Panel panel, ProgramarMantenimientoDto orden, DateTime ahoraUtc)
    {
        panel.ProgramarMantenimiento(orden.Tipo, orden.FechaProgramadaUtc, orden.Responsable, ahoraUtc);
        return ResultadoCambioEstadoPanel.Actualizado;
    }

    internal static async Task<Panel?> BloquearPanelAsync(SolarUyDbContext contexto, Guid panelId, CancellationToken cancelacion)
    {
        var filas = await contexto.Database
            .SqlQuery<int>($"SELECT Estado FROM Paneles WHERE ID = {panelId.ToString("D")} FOR UPDATE")
            .ToListAsync(cancelacion);
        if (filas.Count == 0)
            return null;

        return await contexto.Paneles.Include(panel => panel.Mantenimientos.Where(mantenimiento => mantenimiento.FechaRealizada == null))
            .SingleAsync(panel => panel.ID == panelId, cancelacion);
    }

    internal static PropertyEntry<Panel, long> Revision(SolarUyDbContext contexto, Panel panel) =>
        contexto.Entry(panel).Property<long>(PanelConfiguration.RevisionControl);

    internal static Medicion? AplicarVencimiento(SolarUyDbContext contexto, Panel panel, Medicion? ultima, DateTime ahoraUtc)
    {
        var estadoPrevio = panel.Estado;
        if (!panel.IniciarMantenimientoVencido(ahoraUtc))
            return ultima;

        return RegistrarCambio(contexto, panel, estadoPrevio, ultima, panel.MantenimientoAbierto!.FechaProgramada);
    }

    private static Medicion? RegistrarCambio(SolarUyDbContext contexto, Panel panel, EstadoPanel estadoPrevio,
        Medicion? ultima, DateTime fechaUtc)
    {
        Revision(contexto, panel).CurrentValue++;
        if (panel.Estado == estadoPrevio)
            return ultima;

        var control = Medicion.CrearControl(panel, OrdenarFecha(fechaUtc, ultima));
        contexto.Add(control);
        return control;
    }

    internal static DateTime OrdenarFecha(DateTime fechaUtc, Medicion? ultima) =>
        ultima is not null && ultima.FechaUtc >= fechaUtc ? ultima.FechaUtc.AddTicks(1) : fechaUtc;
}
