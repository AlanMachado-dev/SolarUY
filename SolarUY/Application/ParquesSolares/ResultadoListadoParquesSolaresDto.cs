namespace SolarUY.Application.ParquesSolares;

public sealed record ResultadoListadoParquesSolaresDto(CodigoConsultaParqueSolar Codigo,
    IReadOnlyList<ParqueSolarDto> Parques);
