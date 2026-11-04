using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class MedicionRepository(IDbContextFactory<SolarUyDbContext> contextFactory) : IMedicionRepository
{
    public async Task<Panel?> ObtenerPanelAsync(Guid panelId, CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await contexto.Paneles.AsNoTracking().SingleOrDefaultAsync(panel => panel.ID == panelId, cancellationToken);
    }

    public async Task<Medicion?> ObtenerUltimaAsync(Guid panelId, CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await OrdenarPorRecencia(contexto.Set<Medicion>().AsNoTracking()
            .Include(medicion => medicion.Panel)
            .Where(medicion => EF.Property<Guid>(medicion, "IdPanel") == panelId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken)
    {
        if (panelIds.Count == 0)
            return new Dictionary<Guid, Medicion>();

        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mediciones = contexto.Set<Medicion>().AsNoTracking();
        var ordenadas = OrdenarPorRecencia(mediciones);
        return await mediciones.Include(medicion => medicion.Panel)
            .Where(medicion => panelIds.Contains(EF.Property<Guid>(medicion, "IdPanel")))
            .Where(medicion => medicion.ID == ordenadas
                .Where(candidata => EF.Property<Guid>(candidata, "IdPanel") ==
                    EF.Property<Guid>(medicion, "IdPanel"))
                .Select(candidata => candidata.ID).FirstOrDefault())
            .ToDictionaryAsync(medicion => medicion.Panel.ID, cancellationToken);
    }

    private static IOrderedQueryable<Medicion> OrdenarPorRecencia(IQueryable<Medicion> mediciones) =>
        mediciones.OrderByDescending(medicion => medicion.FechaUtc).ThenByDescending(medicion => medicion.ID);

    public async Task<bool> GuardarAsync(Medicion medicion, CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        contexto.Entry(medicion.Panel).State = EntityState.Unchanged;
        contexto.Add(medicion);
        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
