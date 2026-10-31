using SolarUY.Application.Paneles;

namespace SolarUY.Application.Nodos;

public sealed record ResultadoDetalleNodoDto(CodigoConsultaNodo Codigo, NodoDto? Nodo)
{
    public IReadOnlyList<PanelDto> Paneles { get; init; } = [];
}
