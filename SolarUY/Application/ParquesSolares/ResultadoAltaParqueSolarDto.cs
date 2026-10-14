namespace SolarUY.Application.ParquesSolares;

public enum CodigoErrorAltaParqueSolar
{
    AutenticacionRequerida,
    AccesoDenegado,
    DatosInvalidos,
    NombreDuplicado
}

public sealed record ErrorAltaParqueSolarDto(CodigoErrorAltaParqueSolar Codigo,
    string Mensaje, string? Campo = null);

public sealed record ResultadoAltaParqueSolarDto(ParqueSolarDto? Parque,
    IReadOnlyList<ErrorAltaParqueSolarDto> Errores)
{
    public bool EsExitoso => Parque is not null && Errores.Count == 0;
}
