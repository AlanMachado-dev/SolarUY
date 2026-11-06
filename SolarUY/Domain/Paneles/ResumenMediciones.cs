namespace SolarUY.Domain.Paneles;

/// <summary>Acumulado componible de potencia en W. No representa energía.</summary>
public sealed record ResumenMediciones
{
    public long Cantidad { get; }
    public decimal SumaW { get; }
    public decimal? MinimoW { get; }
    public decimal? MaximoW { get; }
    public decimal? PromedioW => Cantidad == 0 ? null : SumaW / Cantidad;

    private ResumenMediciones(long cantidad, decimal sumaW, decimal? minimoW, decimal? maximoW)
        => (Cantidad, SumaW, MinimoW, MaximoW) = (cantidad, sumaW, minimoW, maximoW);

    public static ResumenMediciones Vacio { get; } = new(0, 0, null, null);

    public static ResumenMediciones Calcular(IEnumerable<decimal> potenciasW)
    {
        ArgumentNullException.ThrowIfNull(potenciasW);
        var resumen = Vacio;
        foreach (var potencia in potenciasW)
        {
            if (potencia < 0) throw new ArgumentOutOfRangeException(nameof(potenciasW));
            resumen = resumen.Componer(new ResumenMediciones(1, potencia, potencia, potencia));
        }
        return resumen;
    }

    public ResumenMediciones Componer(ResumenMediciones otro)
    {
        ArgumentNullException.ThrowIfNull(otro);
        if (Cantidad == 0) return otro;
        if (otro.Cantidad == 0) return this;
        return new(checked(Cantidad + otro.Cantidad), SumaW + otro.SumaW,
            Math.Min(MinimoW!.Value, otro.MinimoW!.Value), Math.Max(MaximoW!.Value, otro.MaximoW!.Value));
    }
}
