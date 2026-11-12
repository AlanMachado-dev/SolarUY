using System.Security.Claims;

namespace SolarUY.Application.Monitoreo;

public sealed class ControlPanelService(IControlPanelRepository repositorio, INotificacionMedicion notificacion)
{
    public async Task<ResultadoControlPanel> CambiarEstadoAsync(
        ClaimsPrincipal usuario, Guid panelId, bool activar, CancellationToken cancellationToken = default)
    {
        if (usuario.Identity?.IsAuthenticated != true || !usuario.IsInRole("Administrador"))
            return new(CodigoControlPanel.AccesoDenegado);

        var resultado = await repositorio.CambiarEstadoAsync(panelId, activar, cancellationToken);
        if (resultado is { Codigo: CodigoControlPanel.Actualizado, Lectura: { } lectura })
            await notificacion.PublicarAsync(lectura);

        return resultado;
    }
}
