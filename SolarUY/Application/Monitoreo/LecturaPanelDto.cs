using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public sealed record RegistrarMedicionDto(Guid PanelId, float GeneracionActualW, EstadoPanel Estado);

public sealed record LecturaPanelDto(
    Guid PanelId, Guid? MedicionId, DateTime? FechaUtc, float? GeneracionActualW,
    EstadoPanel Estado, float PotenciaNominalW)
{
    public bool EsPosteriorA(LecturaPanelDto otra) =>
        Nullable.Compare(FechaUtc, otra.FechaUtc) > 0 ||
        (FechaUtc == otra.FechaUtc && Nullable.Compare(MedicionId, otra.MedicionId) > 0);
}

public enum ResultadoRegistroMedicion
{
    Guardada,
    PanelNoEncontrado,
    LecturaInvalida,
    ErrorDePersistencia
}
