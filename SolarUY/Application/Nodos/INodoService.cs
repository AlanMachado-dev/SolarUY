namespace SolarUY.Application.Nodos;

public interface INodoService
{
    Task<ResultadoDetalleNodoDto> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ResultadoAltaNodoDto> CrearAsync(Guid parqueId, CrearNodoDto datos,
        CancellationToken cancellationToken = default);
}
