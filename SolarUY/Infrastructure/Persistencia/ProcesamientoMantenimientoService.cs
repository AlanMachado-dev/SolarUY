using SolarUY.Application.Monitoreo;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class ProcesamientoMantenimientoService(IServiceScopeFactory fabricaAmbitos,
    TimeProvider reloj, ILogger<ProcesamientoMantenimientoService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var intervalo = new PeriodicTimer(TimeSpan.FromSeconds(1), reloj);
        try
        {
            do
            {
                await ProcesarAsync(stoppingToken);
            } while (await intervalo.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ProcesarAsync(CancellationToken cancelacion)
    {
        try
        {
            await using var ambito = fabricaAmbitos.CreateAsyncScope();
            var servicio = ambito.ServiceProvider.GetRequiredService<VencimientosMantenimientoService>();
            await servicio.ProcesarAsync(cancelacion);
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("No se pudieron procesar los vencimientos de mantenimiento. Se reintentará en el próximo ciclo.");
        }
    }
}
