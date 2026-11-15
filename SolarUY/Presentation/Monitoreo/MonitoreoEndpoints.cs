using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.Monitoreo;

public static class MonitoreoEndpoints
{
    public static void MapearControlSensor(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/monitoreo/paneles/{panelId:guid}/transmision", async (Guid panelId,
            MonitoreoService servicio, HttpContext contexto) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            var permiso = await servicio.ConsultarTransmisionAsync(panelId, contexto.RequestAborted);
            return permiso is null ? Results.NotFound() : Results.Ok(permiso);
        }).RequireAuthorization(politica => politica.RequireRole("Administrador"));
    }
}
