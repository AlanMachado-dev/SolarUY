using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;

// Las verificaciones anteriores a req007 usan datos de prueba sin mediciones.
internal sealed class MedicionRepositorySinLecturas : IMedicionRepository
{
    public Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Medicion>>(new Dictionary<Guid, Medicion>());

    public Task<Panel?> ObtenerPanelAsync(Guid panelId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Medicion?> ObtenerUltimaAsync(Guid panelId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> GuardarAsync(Medicion medicion, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
