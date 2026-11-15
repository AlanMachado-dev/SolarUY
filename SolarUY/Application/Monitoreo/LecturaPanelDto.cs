using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Monitoreo;

public sealed record MuestraSensorDto(Guid PanelId, float? GeneracionActualW, long? RevisionControl);

public sealed record PermisoTransmisionDto(bool Habilitado, long RevisionControl);

public sealed record MantenimientoPanelDto(Guid Id, string Tipo, DateTime FechaProgramadaUtc, string Responsable,
    EstadoMantenimiento Estado);

public enum EstadoMantenimiento { Pendiente, EnCurso }

public sealed record LecturaPanelDto(
    Guid PanelId, Guid? MedicionId, DateTime? FechaUtc, float? GeneracionActualW,
    EstadoPanel Estado, float PotenciaNominalW)
{
    public EstadoPanel EstadoControl { get; init; }
    public long RevisionControl { get; init; }
    public MantenimientoPanelDto? Mantenimiento { get; init; }
    public bool TelemetriaHabilitada => EstadoControl == EstadoPanel.Operativo;
    public float? PotenciaActualW => TelemetriaHabilitada ? GeneracionActualW : 0;

    public static LecturaPanelDto Desde(Panel panel, Medicion? medicion, long revisionControl) =>
        new(panel.ID, medicion?.ID, medicion?.FechaUtc, medicion?.GeneracionActualW,
            medicion?.Estado ?? panel.Estado, panel.PotenciaNominalW)
        {
            EstadoControl = panel.Estado,
            RevisionControl = revisionControl,
            Mantenimiento = panel.MantenimientoAbierto is { } mantenimiento
                ? new(mantenimiento.ID, mantenimiento.Tipo, mantenimiento.FechaProgramada, mantenimiento.Responsable,
                    mantenimiento.EnCurso ? EstadoMantenimiento.EnCurso : EstadoMantenimiento.Pendiente)
                : null
        };

    public bool EsPosteriorA(LecturaPanelDto otra) =>
        RevisionControl > otra.RevisionControl || (RevisionControl == otra.RevisionControl &&
            (Nullable.Compare(FechaUtc, otra.FechaUtc) > 0 ||
             (FechaUtc == otra.FechaUtc && Nullable.Compare(MedicionId, otra.MedicionId) > 0)));
}

public enum ResultadoRegistroMedicion
{
    Guardada,
    PanelNoEncontrado,
    LecturaInvalida,
    EnPausa,
    RevisionDesactualizada,
    ErrorDePersistencia
}

public sealed record ResultadoAdmisionMedicion(ResultadoRegistroMedicion Codigo, LecturaPanelDto? LecturaConfirmada = null);
