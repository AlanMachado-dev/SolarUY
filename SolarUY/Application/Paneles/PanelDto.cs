using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Paneles;

public sealed record PanelDto(Guid ID, Guid NodoId, string Nombre, string Modelo,
    float PotenciaNominalW, DateTime FechaInstalacion, EstadoPanel Estado,
    float? GeneracionActualW, DateTime? UltimoMantenimiento)
{
    internal static PanelDto Desde(Panel panel) => new(panel.ID, panel.Nodo.ID,
        panel.Nombre.Valor, panel.Modelo, panel.PotenciaNominalW, panel.FechaInstalacion,
        panel.Estado, panel.GeneracionActualW, panel.UltimoMantenimiento);
}
