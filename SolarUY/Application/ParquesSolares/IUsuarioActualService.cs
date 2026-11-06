namespace SolarUY.Application.ParquesSolares;

public interface IUsuarioActualService
{
    bool EstaAutenticado { get; }
    bool PerteneceAlRol(string rol);
    Task<SolarUY.Application.Asistente.SesionAsistenteDto?> ObtenerSesionAsistenteAsync(
        CancellationToken cancellationToken) => Task.FromResult<SolarUY.Application.Asistente.SesionAsistenteDto?>(null);
}
