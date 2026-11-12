namespace SolarUY.Application.Energia;

public enum TipoComponenteEnergia
{
    Parque,
    Nodo,
    Panel
}

public enum CoberturaEnergia
{
    Completa,
    Parcial,
    SinDatosSuficientes,
    SinPaneles
}

public sealed record EnergiaEstimadaDto(decimal? EnergiaKwh, CoberturaEnergia Cobertura,
    DateTimeOffset InicioLocal, DateTimeOffset CorteLocal, int PanelesEsperados, int PanelesConDatos);
