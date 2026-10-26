namespace SolarUY.Application.Nodos;

public interface INodoService
{
    Task<ResultadoAltaNodoDto> CrearAsync(Guid parqueId, CrearNodoDto datos,
        CancellationToken cancellationToken = default);
}
