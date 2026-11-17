namespace SolarUY.Application.Monitoreo;

public abstract record OrdenControlPanel;

public sealed record SimularFallaPanel : OrdenControlPanel;
public sealed record SimularArregloPanel : OrdenControlPanel;
public sealed record DesactivarPanel : OrdenControlPanel;
public sealed record ActivarPanel : OrdenControlPanel;

public sealed record ProgramarMantenimientoDto(string? Tipo, DateTime FechaProgramadaUtc, string? Responsable)
    : OrdenControlPanel;

public sealed record FinalizarMantenimientoDto(Guid MantenimientoId, string? Observaciones) : OrdenControlPanel;
