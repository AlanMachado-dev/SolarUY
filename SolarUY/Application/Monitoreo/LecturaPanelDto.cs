using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public sealed record RegistrarMedicionDto(Guid PanelId, float GeneracionActualW, EstadoPanel Estado);

public sealed record LecturaPanelDto(
    Guid PanelId, Guid? MedicionId, DateTime? FechaUtc, float? GeneracionActualW,
    EstadoPanel Estado, float PotenciaNominalW)
{
    public EstadoPanel EstadoControl { get; init; }
    public bool PuedeCambiarEstado => EstadoControl is EstadoPanel.Operativo or EstadoPanel.Desactivado;
    public bool EstaDesactivado => EstadoControl == EstadoPanel.Desactivado;

    public static LecturaPanelDto Desde(Panel panel, Medicion? medicion) =>
        new(panel.ID, medicion?.ID, medicion?.FechaUtc, medicion?.GeneracionActualW,
            medicion?.Estado ?? panel.Estado, panel.PotenciaNominalW)
        {
            EstadoControl = (medicion?.Panel ?? panel).Estado
        };

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
