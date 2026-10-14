namespace SolarUY.Application.ParquesSolares;

public interface IParqueSolarService
{
    Task<ResultadoAltaParqueSolarDto> CrearAsync(CrearParqueSolarDto datos,
        CancellationToken cancellationToken = default);
}
