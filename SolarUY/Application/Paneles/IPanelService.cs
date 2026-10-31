namespace SolarUY.Application.Paneles;

public interface IPanelService
{
    Task<ResultadoAltaPanelDto> CrearAsync(Guid nodoId, CrearPanelDto datos,
        CancellationToken cancellationToken = default);
}
