using System.Security.Claims;
using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public sealed class ControlPanelService(IControlPanelRepository repositorio, INotificacionMedicion notificacion)
{
    public async Task<ResultadoControlPanel> EjecutarAsync(ClaimsPrincipal usuario, Guid panelId,
        OrdenControlPanel orden, CancellationToken cancellationToken = default)
    {
        var autorizado = orden switch
        {
            ProgramarMantenimientoDto or FinalizarMantenimientoDto => PuedeGestionarMantenimiento(usuario),
            SimularFallaPanel or SimularArregloPanel or DesactivarPanel or ActivarPanel => EsAdministrador(usuario),
            _ => false
        };
        if (!autorizado)
            return new(CodigoControlPanel.AccesoDenegado);

        var resultado = await repositorio.EjecutarAsync(panelId, orden, cancellationToken);
        if (resultado is { CambioConfirmado: true, Lectura: { } lectura })
            await notificacion.PublicarAsync(lectura);

        return resultado;
    }

    public static AccionesPanelDto ObtenerAcciones(ClaimsPrincipal usuario, LecturaPanelDto lectura)
    {
        var administrador = EsAdministrador(usuario);
        var mantenimiento = PuedeGestionarMantenimiento(usuario);
        OrdenControlPanel ordenFalla = lectura.EstadoControl == EstadoPanel.EnFalla ? new SimularArregloPanel() : new SimularFallaPanel();
        OrdenControlPanel ordenActivacion = lectura.EstadoControl == EstadoPanel.Desactivado ? new ActivarPanel() : new DesactivarPanel();
        return new(administrador ? ordenFalla : null, administrador ? ordenActivacion : null,
            mantenimiento && lectura.Mantenimiento is null,
            mantenimiento && lectura.Mantenimiento is { Estado: EstadoMantenimiento.EnCurso } abierto ? abierto.Id : null);
    }

    private static bool EsAdministrador(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true && usuario.IsInRole("Administrador");

    private static bool PuedeGestionarMantenimiento(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true && (usuario.IsInRole("Administrador") || usuario.IsInRole("Operador"));
}

public sealed record AccionesPanelDto(OrdenControlPanel? OrdenFalla, OrdenControlPanel? OrdenActivacion,
    bool PuedeProgramar, Guid? MantenimientoParaFinalizar);
