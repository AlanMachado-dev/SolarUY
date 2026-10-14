using SolarUY.Application.ParquesSolares;

namespace SolarUY.Infrastructure.Seguridad;

public sealed class UsuarioActualService : IUsuarioActualService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public UsuarioActualService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public bool EstaAutenticado => _contextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool PerteneceAlRol(string rol) => EstaAutenticado
        && _contextAccessor.HttpContext!.User.IsInRole(rol);
}
