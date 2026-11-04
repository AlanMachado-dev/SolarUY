using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public sealed class MonitoreoService(IMedicionRepository medicionRepository, INotificacionMedicion notificacion)
{
    public async Task<LecturaPanelDto?> ConsultarAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var panel = await medicionRepository.ObtenerPanelAsync(panelId, cancellationToken);
        if (panel is null)
            return null;

        var medicion = await medicionRepository.ObtenerUltimaAsync(panelId, cancellationToken);
        return CrearLectura(panel, medicion);
    }

    public async Task<ResultadoRegistroMedicion> RegistrarAsync(
        RegistrarMedicionDto entrada, CancellationToken cancellationToken = default)
    {
        var panel = await medicionRepository.ObtenerPanelAsync(entrada.PanelId, cancellationToken);
        if (panel is null)
            return ResultadoRegistroMedicion.PanelNoEncontrado;

        Medicion medicion;
        try
        {
            medicion = Medicion.Crear(panel, DateTime.UtcNow, entrada.GeneracionActualW, entrada.Estado);
        }
        catch (ArgumentException)
        {
            return ResultadoRegistroMedicion.LecturaInvalida;
        }

        if (!await medicionRepository.GuardarAsync(medicion, cancellationToken))
            return ResultadoRegistroMedicion.ErrorDePersistencia;

        await notificacion.PublicarAsync(CrearLectura(panel, medicion));
        return ResultadoRegistroMedicion.Guardada;
    }

    private static LecturaPanelDto CrearLectura(Panel panel, Medicion? medicion) =>
        new(panel.ID, medicion?.ID, medicion?.FechaUtc, medicion?.GeneracionActualW,
            medicion?.Estado ?? panel.Estado, panel.PotenciaNominalW);
}
