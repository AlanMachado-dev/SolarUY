namespace SolarUY.Application.Monitoreo;

public interface IPanelesMonitoreados
{
    /// <summary>Devuelve una copia de los identificadores con suscripciones locales activas.</summary>
    IReadOnlyCollection<Guid> ObtenerPaneles();
}
