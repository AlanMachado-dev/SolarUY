using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Energia;

public interface IEnergiaRepository
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<LecturaEnergia>>> ObtenerLecturasPorPanelAsync(
        TipoComponenteEnergia tipo, Guid componenteId, DateTimeOffset desdeUtc, DateTimeOffset corteUtc,
        CancellationToken cancellationToken);
}
