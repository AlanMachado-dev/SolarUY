using SolarUY.Application.Monitoreo;

namespace SolarUY.Infrastructure.Balanceo;

public sealed class SincronizacionMedicionesService(
    IServiceScopeFactory fabricaAmbitos,
    IPanelesMonitoreados panelesMonitoreados,
    INotificacionMedicion notificacion,
    BalanceoOptions opciones,
    ILogger<SincronizacionMedicionesService> logger) : BackgroundService
{
    private readonly Dictionary<Guid, LecturaPanelDto> _ultimas = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromSeconds(opciones.IntervaloSincronizacionSegundos));
        try
        {
            while (await reloj.WaitForNextTickAsync(stoppingToken))
                await SincronizarAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task SincronizarAsync(CancellationToken cancelacion)
    {
        var paneles = panelesMonitoreados.ObtenerPaneles().ToHashSet();
        foreach (var panelId in _ultimas.Keys.Where(id => !paneles.Contains(id)).ToArray())
            _ultimas.Remove(panelId);
        if (paneles.Count == 0)
            return;

        await using var ambito = fabricaAmbitos.CreateAsyncScope();
        var servicio = ambito.ServiceProvider.GetRequiredService<MonitoreoService>();
        foreach (var panelId in paneles)
        {
            try
            {
                var lectura = await servicio.ConsultarAsync(panelId, cancelacion);
                if (lectura is null || (_ultimas.TryGetValue(panelId, out var anterior) && !lectura.EsPosteriorA(anterior)))
                    continue;
                await notificacion.PublicarAsync(lectura);
                _ultimas[panelId] = lectura;
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                logger.LogWarning("No se pudo sincronizar el monitoreo. Código: sincronizacion_consulta.");
            }
        }
    }
}
