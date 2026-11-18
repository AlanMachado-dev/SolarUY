using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.Monitoreo;

[Authorize(Policy = PoliticasMonitoreo.SensorSimulado)]
public sealed class MonitoreoHub(MonitoreoService monitoreoService, NotificacionMedicion notificacion,
    IHubContext<MonitoreoHub> hub) : Hub
{
    private static readonly object SuscripcionSensor = new();

    public async Task<PermisoTransmisionDto> AsociarSensor(Guid panelId)
    {
        var clave = Context.ConnectionId;
        var cliente = hub.Clients.Client(clave);
        var cancelacion = Context.ConnectionAborted;
        RetirarSensor();
        var suscripcion = notificacion.Suscribir(Context.User!, panelId, lectura =>
            lectura.TelemetriaHabilitada
                ? Task.CompletedTask
                : cliente.SendAsync("PausarTelemetria", lectura.RevisionControl, cancelacion));
        Context.Items[SuscripcionSensor] = (panelId, suscripcion);

        try
        {
            return await monitoreoService.ConsultarTransmisionAsync(panelId, cancelacion)
                ?? throw new HubException("PanelNoEncontrado");
        }
        catch
        {
            RetirarSensor();
            throw;
        }
    }

    public async Task<string> Registrar(MuestraSensorDto entrada)
    {
        if (!Context.Items.TryGetValue(SuscripcionSensor, out var valor) ||
            valor is not ValueTuple<Guid, IDisposable> sensor || sensor.Item1 != entrada.PanelId)
            throw new HubException("SensorNoAsociado");

        return (await monitoreoService.RegistrarAsync(entrada, Context.ConnectionAborted)).ToString();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        RetirarSensor();
        return base.OnDisconnectedAsync(exception);
    }

    private void RetirarSensor()
    {
        if (Context.Items.Remove(SuscripcionSensor, out var valor) && valor is ValueTuple<Guid, IDisposable> sensor)
            sensor.Item2.Dispose();
    }
}
