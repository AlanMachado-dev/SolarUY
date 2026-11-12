namespace SolarUY.Domain.Paneles;

public sealed record LecturaEnergia(Guid Id, DateTimeOffset FechaUtc, decimal PotenciaW);

public sealed class EnergiaEstimadaPanel
{
    public static TimeSpan MaximaSeparacionEntreLecturas { get; } = TimeSpan.FromSeconds(40);

    public decimal? EnergiaKwh { get; }
    public bool EsParcial { get; }

    private EnergiaEstimadaPanel(decimal? energiaKwh, bool esParcial)
        => (EnergiaKwh, EsParcial) = (energiaKwh, esParcial);

    public static EnergiaEstimadaPanel Calcular(IEnumerable<LecturaEnergia> lecturas,
        DateTimeOffset inicioUtc, DateTimeOffset corteUtc)
    {
        ArgumentNullException.ThrowIfNull(lecturas);
        if (corteUtc < inicioUtc)
            throw new ArgumentOutOfRangeException(nameof(corteUtc));

        var ordenadas = lecturas.Where(lectura => lectura.FechaUtc <= corteUtc)
            .OrderBy(lectura => lectura.FechaUtc)
            .ThenByDescending(lectura => lectura.Id)
            .DistinctBy(lectura => lectura.FechaUtc)
            .ToArray();

        decimal potenciaPorTicks = 0;
        DateTimeOffset? finCobertura = null;
        var esParcial = false;

        for (var i = 1; i < ordenadas.Length; i++)
        {
            var anterior = ordenadas[i - 1];
            var finTramo = ordenadas[i].FechaUtc;
            if (finTramo <= inicioUtc || finTramo - anterior.FechaUtc > MaximaSeparacionEntreLecturas)
                continue;

            if (anterior.PotenciaW < 0)
                throw new ArgumentOutOfRangeException(nameof(lecturas), "La potencia debe ser mayor o igual a cero.");

            var inicioTramo = anterior.FechaUtc < inicioUtc ? inicioUtc : anterior.FechaUtc;
            esParcial |= inicioTramo > (finCobertura ?? inicioUtc);
            potenciaPorTicks += anterior.PotenciaW * (finTramo - inicioTramo).Ticks;
            finCobertura = finTramo;
        }

        if (finCobertura is not { } fin)
            return new EnergiaEstimadaPanel(null, false);

        var energiaKwh = potenciaPorTicks / (TimeSpan.TicksPerHour * 1_000m);
        return new EnergiaEstimadaPanel(energiaKwh, esParcial || corteUtc - fin > MaximaSeparacionEntreLecturas);
    }
}
