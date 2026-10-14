namespace SolarUY.Application.ParquesSolares;

public interface IUsuarioActualService
{
    bool EstaAutenticado { get; }
    bool PerteneceAlRol(string rol);
}
