namespace SolarUY.Application.Monitoreo;

public interface IControlPanelRepository
{
    Task<ResultadoControlPanel> CambiarEstadoAsync(Guid panelId, bool activar, CancellationToken cancellationToken);
}

public sealed record ResultadoControlPanel(CodigoControlPanel Codigo, LecturaPanelDto? Lectura = null);

public enum CodigoControlPanel
{
    Actualizado,
    SinCambios,
    AccesoDenegado,
    PanelNoEncontrado,
    EstadoNoPermitido,
    ErrorDePersistencia
}
