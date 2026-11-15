namespace SolarUY.Application.Monitoreo;

public sealed class MonitoreoService(IMedicionRepository medicionRepository, INotificacionMedicion notificacion)
{
    public Task<LecturaPanelDto?> ConsultarAsync(Guid panelId, CancellationToken cancellationToken = default) =>
        medicionRepository.ConsultarAsync(panelId, cancellationToken);

    public async Task<PermisoTransmisionDto?> ConsultarTransmisionAsync(Guid panelId,
        CancellationToken cancellationToken = default)
    {
        var confirmada = await ConsultarAsync(panelId, cancellationToken);
        return confirmada is null ? null : new(confirmada.TelemetriaHabilitada, confirmada.RevisionControl);
    }

    public async Task<ResultadoRegistroMedicion> RegistrarAsync(MuestraSensorDto entrada,
        CancellationToken cancellationToken = default)
    {
        var resultado = await medicionRepository.AdmitirAsync(entrada, cancellationToken);
        if (resultado.LecturaConfirmada is { } lectura)
            await notificacion.PublicarAsync(lectura);

        return resultado.Codigo;
    }
}
