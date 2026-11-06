using System.Collections.ObjectModel;

namespace SolarUY.Domain.Paneles;

public sealed record LecturaObservada(DateTimeOffset FechaUtc, decimal PotenciaW, EstadoPanel Estado);
public sealed record ConteoEstadoObservado(EstadoPanel Estado, int Cantidad,
    DateTimeOffset PrimeraFechaUtc, DateTimeOffset UltimaFechaUtc);

public sealed class EstadoActualObservado
{
    public decimal? PotenciaVigenteW { get; }
    public int PanelesVigentes { get; }
    public int PanelesAntiguos { get; }
    public int PanelesSinMedicion { get; }
    public IReadOnlyList<ConteoEstadoObservado> Estados { get; }

    private EstadoActualObservado(LecturaObservada?[] lecturas, DateTimeOffset instanteUtc, TimeSpan vigencia)
    {
        var observadas = lecturas.OfType<LecturaObservada>().ToArray();
        var vigentes = observadas.Where(lectura => instanteUtc - lectura.FechaUtc <= vigencia).ToArray();
        PanelesVigentes = vigentes.Length;
        PanelesAntiguos = observadas.Length - vigentes.Length;
        PanelesSinMedicion = lecturas.Length - observadas.Length;
        PotenciaVigenteW = vigentes.Length == 0 ? null : vigentes.Sum(lectura => lectura.PotenciaW);
        Estados = new ReadOnlyCollection<ConteoEstadoObservado>(observadas.GroupBy(lectura => lectura.Estado)
            .Select(grupo => new ConteoEstadoObservado(grupo.Key, grupo.Count(),
                grupo.Min(lectura => lectura.FechaUtc), grupo.Max(lectura => lectura.FechaUtc))).ToArray());
    }

    public static EstadoActualObservado Calcular(IEnumerable<LecturaObservada?> ultimasLecturas,
        DateTimeOffset instanteUtc, TimeSpan vigencia)
    {
        ArgumentNullException.ThrowIfNull(ultimasLecturas);
        if (vigencia < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(vigencia));
        var lecturas = ultimasLecturas.ToArray();
        if (lecturas.OfType<LecturaObservada>().Any(lectura => lectura.FechaUtc > instanteUtc
            || lectura.PotenciaW < 0 || !Enum.IsDefined(lectura.Estado)))
            throw new ArgumentException("Las lecturas deben ser válidas y anteriores al instante de consulta.", nameof(ultimasLecturas));
        return new(lecturas, instanteUtc, vigencia);
    }
}
