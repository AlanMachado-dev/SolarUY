using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Paneles;

public enum ResultadoGuardadoPanel
{
    Guardado,
    ConflictoNombre,
    NodoNoEncontrado
}

public interface IPanelRepository
{
    Task<ResultadoGuardadoPanel> GuardarAsync(Panel panel, CancellationToken cancellationToken = default);
}
