using SolarUY.Domain.Nodos;

namespace SolarUY.Domain.Paneles;

public sealed class Panel
{
    public Guid ID { get; private set; }
    public NombrePanel Nombre { get; private set; } = null!;
    public string Modelo { get; private set; } = string.Empty;
    public float PotenciaNominalW { get; private set; }
    public DateTime FechaInstalacion { get; private set; }
    public EstadoPanel Estado { get; private set; }
    public Nodo Nodo { get; private set; } = null!;
    public float? GeneracionActualW => null;
    public DateTime? UltimoMantenimiento => null;

    private Panel() { }

    internal static Panel Crear(string? nombre, string? modelo, float potenciaNominalW,
        DateTime instanteUtc, Nodo? nodo)
    {
        var nombrePanel = new NombrePanel(nombre);
        if (string.IsNullOrWhiteSpace(modelo))
            throw new DatosPanelInvalidosException("Modelo", "El modelo es obligatorio.");
        if (!float.IsFinite(potenciaNominalW) || potenciaNominalW <= 0)
            throw new DatosPanelInvalidosException("PotenciaNominalW", "La potencia nominal debe ser finita y mayor que cero.");
        if (nodo is null)
            throw new DatosPanelInvalidosException("Nodo", "El nodo es obligatorio.");
        if (instanteUtc.Kind != DateTimeKind.Utc)
            throw new DatosPanelInvalidosException("FechaInstalacion", "El instante de instalación debe estar expresado en UTC.");

        return new Panel
        {
            ID = Guid.NewGuid(), Nombre = nombrePanel, Modelo = modelo.Trim(),
            PotenciaNominalW = potenciaNominalW,
            FechaInstalacion = new DateTime(instanteUtc.Ticks - instanteUtc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc),
            Estado = EstadoPanel.Operativo, Nodo = nodo
        };
    }
}
