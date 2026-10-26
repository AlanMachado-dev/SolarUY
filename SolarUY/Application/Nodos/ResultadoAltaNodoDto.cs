namespace SolarUY.Application.Nodos;

public enum CodigoErrorAltaNodo
{
    AutenticacionRequerida,
    AccesoDenegado,
    ParqueNoEncontrado,
    DatosInvalidos,
    NumeroSerieInversorDuplicado,
    NumeroSerieSensorDuplicado
}

public sealed record ErrorAltaNodoDto(CodigoErrorAltaNodo Codigo, string Mensaje, string? Campo = null);

public sealed record ResultadoAltaNodoDto(NodoDto? Nodo, IReadOnlyList<ErrorAltaNodoDto> Errores)
{
    public bool EsExitoso => Nodo is not null && Errores.Count == 0;
}
