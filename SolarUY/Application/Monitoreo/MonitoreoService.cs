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
        return LecturaPanelDto.Desde(panel, medicion);
    }

    public async Task<bool?> ConsultarHabilitadoAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var panel = await medicionRepository.ObtenerPanelAsync(panelId, cancellationToken);
        return panel is null ? null : panel.Estado != EstadoPanel.Desactivado;
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

        await notificacion.PublicarAsync(LecturaPanelDto.Desde(panel, medicion));
        return ResultadoRegistroMedicion.Guardada;
    }
}
