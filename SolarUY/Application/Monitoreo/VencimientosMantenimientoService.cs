namespace SolarUY.Application.Monitoreo;

public sealed class VencimientosMantenimientoService(IControlPanelRepository repositorio,
    INotificacionMedicion notificacion, TimeProvider reloj)
{
    public async Task ProcesarAsync(CancellationToken cancellationToken)
    {
        var paneles = await repositorio.BuscarVencidosAsync(reloj.GetUtcNow().UtcDateTime, cancellationToken);
        var fallo = false;
        foreach (var panelId in paneles)
        {
            var resultado = await repositorio.IniciarVencidoAsync(panelId, cancellationToken);
            if (resultado is { CambioConfirmado: true, Lectura: { } lectura })
                await notificacion.PublicarAsync(lectura);
            fallo |= resultado.Codigo == CodigoControlPanel.ErrorDePersistencia;
        }

        if (fallo)
            throw new InvalidOperationException("No se pudieron guardar todos los inicios de mantenimiento.");
    }
}
