using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.Monitoreo;

[Authorize(Roles = "Administrador")]
public sealed class MonitoreoHub(MonitoreoService monitoreoService) : Hub
{
    public async Task Registrar(RegistrarMedicionDto entrada)
    {
        var resultado = await monitoreoService.RegistrarAsync(entrada, Context.ConnectionAborted);
        if (resultado != ResultadoRegistroMedicion.Guardada)
            throw new HubException(resultado.ToString());
    }
}
