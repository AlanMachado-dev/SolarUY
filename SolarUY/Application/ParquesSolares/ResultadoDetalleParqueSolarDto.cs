using SolarUY.Application.Nodos;

namespace SolarUY.Application.ParquesSolares;

public sealed record ResultadoDetalleParqueSolarDto(CodigoConsultaParqueSolar Codigo,
    ParqueSolarDto? Parque, IReadOnlyList<NodoDto> Nodos);
