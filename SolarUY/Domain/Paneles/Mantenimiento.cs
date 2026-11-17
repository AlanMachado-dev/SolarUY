namespace SolarUY.Domain.Paneles;

public sealed class Mantenimiento
{
    public Guid ID { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public string Tipo { get; private set; } = string.Empty;
    public DateTime FechaProgramada { get; private set; }
    public string Responsable { get; private set; } = string.Empty;
    public DateTime? InicioProcesadoUtc { get; private set; }
    public DateTime? FechaRealizada { get; private set; }
    public string? Observaciones { get; private set; }
    public bool EnCurso => InicioProcesadoUtc.HasValue && !FechaRealizada.HasValue;

    private Mantenimiento() { }

    internal static Mantenimiento Programar(Panel panel, string? tipo, DateTime fechaProgramada, string? responsable)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new DatosPanelInvalidosException(nameof(Tipo), "El tipo de mantenimiento es obligatorio.");
        if (string.IsNullOrWhiteSpace(responsable))
            throw new DatosPanelInvalidosException(nameof(Responsable), "El responsable es obligatorio.");
        if (fechaProgramada == default || fechaProgramada.Kind != DateTimeKind.Utc)
            throw new DatosPanelInvalidosException(nameof(FechaProgramada), "Ingresa una fecha programada válida en UTC.");

        return new Mantenimiento
        {
            ID = Guid.NewGuid(), Panel = panel, Tipo = tipo.Trim(),
            FechaProgramada = fechaProgramada, Responsable = responsable.Trim()
        };
    }

    internal bool Iniciar(DateTime ahoraUtc)
    {
        if (InicioProcesadoUtc.HasValue || FechaRealizada.HasValue || ahoraUtc < FechaProgramada)
            return false;

        InicioProcesadoUtc = ahoraUtc;
        return true;
    }

    internal ResultadoCambioEstadoPanel Finalizar(DateTime ahoraUtc, string? observaciones)
    {
        if (FechaRealizada.HasValue)
            return ResultadoCambioEstadoPanel.SinCambios;
        if (!EnCurso || ahoraUtc < InicioProcesadoUtc)
            return ResultadoCambioEstadoPanel.NoPermitido;

        FechaRealizada = ahoraUtc;
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        return ResultadoCambioEstadoPanel.Actualizado;
    }
}
