using SolarUY.Domain.Nodos;

namespace SolarUY.Application.Nodos;

public enum ResultadoGuardadoNodo
{
    Guardado,
    ParqueNoEncontrado,
    ConflictoNumeroSerieInversor,
    ConflictoNumeroSerieSensor
}

public interface INodoRepository
{
    Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExisteNumeroSerieInversorAsync(string numeroSerie, CancellationToken cancellationToken = default);
    Task<bool> ExisteNumeroSerieSensorAsync(string numeroSerie, CancellationToken cancellationToken = default);
    Task<ResultadoGuardadoNodo> GuardarAsync(Nodo nodo, CancellationToken cancellationToken = default);
}
