namespace SolarUY.Application.Paneles;

public enum CodigoErrorAltaPanel
{
    AutenticacionRequerida,
    AccesoDenegado,
    NodoNoEncontrado,
    DatosInvalidos,
    NombreDuplicado
}

public sealed record ErrorAltaPanelDto(CodigoErrorAltaPanel Codigo, string Mensaje, string? Campo = null);

public sealed record ResultadoAltaPanelDto(PanelDto? Panel, IReadOnlyList<ErrorAltaPanelDto> Errores)
{
    public bool EsExitoso => Panel is not null && Errores.Count == 0;
}
