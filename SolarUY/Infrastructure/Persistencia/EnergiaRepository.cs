using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Energia;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class EnergiaRepository(IDbContextFactory<SolarUyDbContext> contextFactory) : IEnergiaRepository
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<LecturaEnergia>>> ObtenerLecturasPorPanelAsync(
        TipoComponenteEnergia tipo, Guid componenteId, DateTimeOffset desdeUtc, DateTimeOffset corteUtc,
        CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        var paneles = contexto.Paneles.AsNoTracking();
        var panelesDelComponente = tipo switch
        {
            TipoComponenteEnergia.Parque => paneles.Where(panel => panel.Nodo.Parque.ID == componenteId),
            TipoComponenteEnergia.Nodo => paneles.Where(panel => panel.Nodo.ID == componenteId),
            TipoComponenteEnergia.Panel => paneles.Where(panel => panel.ID == componenteId),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

        var idsDelComponente = panelesDelComponente.Select(panel => panel.ID);
        var panelIds = await idsDelComponente.ToArrayAsync(cancellationToken);
        if (panelIds.Length == 0)
            return new Dictionary<Guid, IReadOnlyList<LecturaEnergia>>();

        var desde = desdeUtc.UtcDateTime;
        var corte = corteUtc.UtcDateTime;
        var filas = await contexto.Set<Medicion>().AsNoTracking()
            .Where(medicion => idsDelComponente.Contains(EF.Property<Guid>(medicion, "IdPanel"))
                && medicion.FechaUtc >= desde && medicion.FechaUtc <= corte)
            .OrderBy(medicion => EF.Property<Guid>(medicion, "IdPanel"))
            .ThenBy(medicion => medicion.FechaUtc)
            .ThenBy(medicion => medicion.ID)
            .Select(medicion => new
            {
                PanelId = EF.Property<Guid>(medicion, "IdPanel"),
                medicion.ID,
                medicion.FechaUtc,
                medicion.GeneracionActualW
            })
            .ToListAsync(cancellationToken);

        var lecturasPorPanel = filas.ToLookup(fila => fila.PanelId,
            fila => new LecturaEnergia(fila.ID, new DateTimeOffset(fila.FechaUtc), (decimal)fila.GeneracionActualW));
        return panelIds.ToDictionary(id => id, id => (IReadOnlyList<LecturaEnergia>)lecturasPorPanel[id].ToArray());
    }
}
