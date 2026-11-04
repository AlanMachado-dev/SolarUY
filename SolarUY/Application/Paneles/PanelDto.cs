using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Paneles;

public sealed record PanelDto(Guid ID, Guid NodoId, string Nombre, string Modelo,
    float PotenciaNominalW, DateTime FechaInstalacion, EstadoPanel Estado,
    float? GeneracionActualW, DateTime? UltimoMantenimiento)
{
    public MedicionPanelDto? UltimaMedicion { get; init; }

    internal static PanelDto Desde(Panel panel, Medicion? medicion = null) => new(panel.ID, panel.Nodo.ID,
        panel.Nombre.Valor, panel.Modelo, panel.PotenciaNominalW, panel.FechaInstalacion,
        panel.Estado, medicion?.GeneracionActualW ?? panel.GeneracionActualW, panel.UltimoMantenimiento)
    {
        UltimaMedicion = medicion is null ? null : new(medicion.GeneracionActualW, medicion.Estado, medicion.FechaUtc)
    };
}

public sealed record MedicionPanelDto(float GeneracionActualW, EstadoPanel Estado, DateTime FechaUtc);
