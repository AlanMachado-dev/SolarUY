using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;

internal sealed class MedicionRepositorySinLecturas : IMedicionRepository
{
    public Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Medicion>>(new Dictionary<Guid, Medicion>());

    public Task<LecturaPanelDto?> ConsultarAsync(Guid panelId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ResultadoAdmisionMedicion> AdmitirAsync(MuestraSensorDto muestra, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
