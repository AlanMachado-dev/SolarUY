using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public interface IMedicionRepository
{
    Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken);
    Task<LecturaPanelDto?> ConsultarAsync(Guid panelId, CancellationToken cancellationToken);
    Task<ResultadoAdmisionMedicion> AdmitirAsync(MuestraSensorDto muestra, CancellationToken cancellationToken);
}
