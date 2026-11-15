namespace SolarUY.Application.Monitoreo;

public interface IControlPanelRepository
{
    Task<ResultadoControlPanel> EjecutarAsync(Guid panelId, OrdenControlPanel orden, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> BuscarVencidosAsync(DateTime ahoraUtc, CancellationToken cancellationToken);
    Task<ResultadoControlPanel> IniciarVencidoAsync(Guid panelId, CancellationToken cancellationToken);
}

public sealed record ResultadoControlPanel(CodigoControlPanel Codigo, LecturaPanelDto? Lectura = null,
    bool CambioConfirmado = false, string? Error = null);

public enum CodigoControlPanel
{
    Actualizado,
    SinCambios,
    AccesoDenegado,
    PanelNoEncontrado,
    EstadoNoPermitido,
    DatosInvalidos,
    ErrorDePersistencia
}
