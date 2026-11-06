namespace SolarUY.Domain.Paneles;

public sealed record PotenciaObservada
{
    public int PanelesEsperados { get; }
    public int PanelesConMuestras { get; }
    public decimal? PromedioObservadoW { get; }
    public bool EsParcial { get; }

    private PotenciaObservada(int esperados, int observados, decimal? promedioW, bool parcial)
        => (PanelesEsperados, PanelesConMuestras, PromedioObservadoW, EsParcial) =
            (esperados, observados, promedioW, parcial);

    public static PotenciaObservada Agregar(IEnumerable<ResumenMediciones> paneles,
        int panelesEsperados, bool coberturaCompleta)
    {
        ArgumentNullException.ThrowIfNull(paneles);
        var resumentes = paneles.ToArray();
        if (panelesEsperados < resumentes.Length || panelesEsperados < 0)
            throw new ArgumentOutOfRangeException(nameof(panelesEsperados));
        var observados = resumentes.Where(resumen => resumen.Cantidad > 0).ToArray();
        return new(panelesEsperados, observados.Length,
            observados.Length == 0 ? null : observados.Sum(resumen => resumen.PromedioW!.Value),
            !coberturaCompleta || observados.Length != panelesEsperados || panelesEsperados == 0);
    }

    public ComparacionPotencia CompararCon(PotenciaObservada referencia) =>
        ComparacionPotencia.Crear(this, referencia);
}

public sealed record ComparacionPotencia(decimal? DiferenciaW, decimal? DiferenciaPorcentual,
    bool EsParcial, string Conclusion)
{
    public string? CambioObservado => DiferenciaW is null ? null :
        DiferenciaW > 0 ? "Aumento" : DiferenciaW < 0 ? "Disminucion" : "Igualdad";

    internal static ComparacionPotencia Crear(PotenciaObservada actual, PotenciaObservada referencia)
    {
        var diferencia = actual.PromedioObservadoW - referencia.PromedioObservadoW;
        var parcial = actual.EsParcial || referencia.EsParcial || diferencia is null
            || actual.PanelesEsperados != referencia.PanelesEsperados;
        var porcentaje = referencia.PromedioObservadoW is null or 0 ? null :
            diferencia / referencia.PromedioObservadoW * 100;
        return new(diferencia, porcentaje, parcial,
            parcial ? "NoConcluyente" : diferencia > 0 ? "Aumento" : diferencia < 0 ? "Disminucion" : "Igualdad");
    }
}
