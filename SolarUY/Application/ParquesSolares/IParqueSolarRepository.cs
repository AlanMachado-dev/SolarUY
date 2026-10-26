using SolarUY.Domain.ParquesSolares;

namespace SolarUY.Application.ParquesSolares;

public enum ResultadoGuardadoParqueSolar
{
    Guardado,
    ConflictoNombre
}

public interface IParqueSolarRepository
{
    Task<IReadOnlyList<ParqueSolar>> ListarAsync(CancellationToken cancellationToken = default);
    Task<ParqueSolar?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExisteNombreAsync(string claveNombre, CancellationToken cancellationToken = default);
    Task<ResultadoGuardadoParqueSolar> GuardarAsync(ParqueSolar parque,
        CancellationToken cancellationToken = default);
}
