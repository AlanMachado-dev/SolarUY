using SolarUY.Domain.Nodos;

namespace SolarUY.Domain.Paneles;

public sealed class Panel
{
    private readonly List<Mantenimiento> _mantenimientos = [];

    public Guid ID { get; private set; }
    public NombrePanel Nombre { get; private set; } = null!;
    public string Modelo { get; private set; } = string.Empty;
    public float PotenciaNominalW { get; private set; }
    public DateTime FechaInstalacion { get; private set; }
    public EstadoPanel Estado { get; private set; }
    public Nodo Nodo { get; private set; } = null!;
    public bool AdmiteTelemetria => Estado == EstadoPanel.Operativo;
    public float? GeneracionActualW => AdmiteTelemetria ? null : 0;
    public IReadOnlyCollection<Mantenimiento> Mantenimientos => _mantenimientos.AsReadOnly();
    public Mantenimiento? MantenimientoAbierto => _mantenimientos.SingleOrDefault(mantenimiento => !mantenimiento.FechaRealizada.HasValue);
    public DateTime? UltimoMantenimiento => _mantenimientos.Max(mantenimiento => mantenimiento.FechaRealizada);

    private Panel() { }

    public Mantenimiento ProgramarMantenimiento(string? tipo, DateTime fechaProgramadaUtc, string? responsable,
        DateTime ahoraUtc)
    {
        ValidarInstante(ahoraUtc);
        if (MantenimientoAbierto is not null)
            throw new DatosPanelInvalidosException(nameof(Mantenimientos), "El panel ya tiene un mantenimiento sin finalizar.");

        var mantenimiento = Mantenimiento.Programar(this, tipo, fechaProgramadaUtc, responsable);
        _mantenimientos.Add(mantenimiento);
        IniciarMantenimientoVencido(ahoraUtc);
        return mantenimiento;
    }

    public bool IniciarMantenimientoVencido(DateTime ahoraUtc)
    {
        ValidarInstante(ahoraUtc);
        if (MantenimientoAbierto?.Iniciar(ahoraUtc) != true)
            return false;

        if (Estado == EstadoPanel.Operativo)
            Estado = EstadoPanel.EnMantenimiento;

        return true;
    }

    public ResultadoCambioEstadoPanel FinalizarMantenimiento(Guid mantenimientoId, DateTime ahoraUtc, string? observaciones)
    {
        ValidarInstante(ahoraUtc);
        var mantenimiento = _mantenimientos.Find(registro => registro.ID == mantenimientoId);
        if (mantenimiento is null)
            return ResultadoCambioEstadoPanel.NoPermitido;

        var resultado = mantenimiento.Finalizar(ahoraUtc, observaciones);
        if (resultado == ResultadoCambioEstadoPanel.Actualizado && Estado == EstadoPanel.EnMantenimiento)
            Estado = EstadoPanel.Operativo;

        return resultado;
    }

    public ResultadoCambioEstadoPanel SimularFalla() => CambiarEstado(EstadoPanel.EnFalla);
    public ResultadoCambioEstadoPanel Desactivar() => CambiarEstado(EstadoPanel.Desactivado);
    public ResultadoCambioEstadoPanel SimularArreglo() => RecuperarDesde(EstadoPanel.EnFalla);
    public ResultadoCambioEstadoPanel Activar() => RecuperarDesde(EstadoPanel.Desactivado);

    private ResultadoCambioEstadoPanel RecuperarDesde(EstadoPanel estadoDeOrigen)
    {
        var destino = MantenimientoAbierto?.EnCurso == true ? EstadoPanel.EnMantenimiento : EstadoPanel.Operativo;
        if (Estado == destino)
            return ResultadoCambioEstadoPanel.SinCambios;

        return Estado == estadoDeOrigen ? CambiarEstado(destino) : ResultadoCambioEstadoPanel.NoPermitido;
    }

    private ResultadoCambioEstadoPanel CambiarEstado(EstadoPanel destino)
    {
        if (Estado == destino)
            return ResultadoCambioEstadoPanel.SinCambios;

        Estado = destino;
        return ResultadoCambioEstadoPanel.Actualizado;
    }

    private static void ValidarInstante(DateTime ahoraUtc)
    {
        if (ahoraUtc == default || ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("El instante de control debe estar en UTC.", nameof(ahoraUtc));
    }

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
