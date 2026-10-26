namespace SolarUY.Application.ParquesSolares;

public interface IParqueSolarService
{
    Task<ResultadoListadoParquesSolaresDto> ListarAsync(CancellationToken cancellationToken = default);
    Task<ResultadoDetalleParqueSolarDto> ObtenerPorIdAsync(Guid id,
        CancellationToken cancellationToken = default);
    Task<ResultadoAltaParqueSolarDto> CrearAsync(CrearParqueSolarDto datos,
        CancellationToken cancellationToken = default);
}
