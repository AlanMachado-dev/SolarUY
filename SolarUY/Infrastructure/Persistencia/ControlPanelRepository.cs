using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class ControlPanelRepository(
    IDbContextFactory<SolarUyDbContext> contextFactory, TimeProvider reloj) : IControlPanelRepository
{
    public async Task<ResultadoControlPanel> CambiarEstadoAsync(
        Guid panelId, bool activar, CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancellationToken);
        if (await MedicionRepository.BloquearEstadoPanelAsync(contexto, panelId, cancellationToken) is null)
            return new(CodigoControlPanel.PanelNoEncontrado);

        var panel = await contexto.Paneles.SingleAsync(panel => panel.ID == panelId, cancellationToken);
        var cambio = panel.CambiarEstadoOperativo(activar);
        if (cambio == ResultadoCambioEstadoPanel.SinCambios)
            return new(CodigoControlPanel.SinCambios);
        if (cambio == ResultadoCambioEstadoPanel.NoPermitido)
            return new(CodigoControlPanel.EstadoNoPermitido);

        var ultimaFecha = await contexto.Set<Medicion>()
            .Where(medicion => EF.Property<Guid>(medicion, "IdPanel") == panelId)
            .OrderByDescending(medicion => medicion.FechaUtc)
            .Select(medicion => (DateTime?)medicion.FechaUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var fecha = reloj.GetUtcNow().UtcDateTime;
        if (ultimaFecha is { } anterior && fecha <= anterior)
            fecha = anterior.AddTicks(1);

        var medicion = Medicion.Crear(panel, fecha, 0, panel.Estado);
        contexto.Add(medicion);
        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            return new(CodigoControlPanel.Actualizado, LecturaPanelDto.Desde(panel, medicion));
        }
        catch (DbUpdateException)
        {
            return new(CodigoControlPanel.ErrorDePersistencia);
        }
    }
}
