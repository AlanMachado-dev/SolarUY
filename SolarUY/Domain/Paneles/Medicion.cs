namespace SolarUY.Domain.Paneles;

public sealed class Medicion
{
    public Guid ID { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public DateTime FechaUtc { get; private set; }
    public float GeneracionActualW { get; private set; }
    public EstadoPanel Estado { get; private set; }

    private Medicion() { }

    public static Medicion CrearLectura(Panel panel, DateTime fechaUtc, float generacionActualW)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!panel.AdmiteTelemetria)
            throw new InvalidOperationException("El panel tiene la telemetría en pausa.");

        return Crear(panel, fechaUtc, generacionActualW, EstadoPanel.Operativo);
    }

    public static Medicion CrearControl(Panel panel, DateTime fechaUtc) => Crear(panel, fechaUtc, 0, panel.Estado);

    public static Medicion Crear(Panel panel, DateTime fechaUtc, float generacionActualW, EstadoPanel estado)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (fechaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de medición debe estar en UTC.", nameof(fechaUtc));
        if (!float.IsFinite(generacionActualW) || generacionActualW < 0)
            throw new ArgumentOutOfRangeException(nameof(generacionActualW));
        if (!Enum.IsDefined(estado))
            throw new ArgumentOutOfRangeException(nameof(estado));

        return new Medicion
        {
            ID = Guid.NewGuid(),
            Panel = panel,
            FechaUtc = fechaUtc,
            GeneracionActualW = generacionActualW,
            Estado = estado
        };
    }
}
