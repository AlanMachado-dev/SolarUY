using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public interface IMedicionRepository
{
    Task<Panel?> ObtenerPanelAsync(Guid panelId, CancellationToken cancellationToken);
    Task<Medicion?> ObtenerUltimaAsync(Guid panelId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken);
    Task<bool> GuardarAsync(Medicion medicion, CancellationToken cancellationToken);
}
