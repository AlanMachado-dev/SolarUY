using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Asistente;

/// <summary>Compone acumulados de tramos contiguos dentro del mismo turno; nunca recibe acumulados del navegador.</summary>
public static class ComposicionResumenService
{
    public static InstantaneaConsultaDto Componer(InstantaneaConsultaDto anterior, InstantaneaConsultaDto siguiente,
        PeriodoConsultaDto periodo, DateTimeOffset instante, int umbralSegundos)
    {
        var elementos = anterior.Elementos.Concat(siguiente.Elementos).DistinctBy(elemento => (elemento.Tipo, elemento.Id)).ToArray();
        var series = anterior.Series.Concat(siguiente.Series).GroupBy(serie => serie.Panel.Id).Select(grupo =>
        {
            var tramos = grupo.ToArray();
            var resumen = tramos.Aggregate(ResumenMediciones.Vacio, (acumulado, tramo) => acumulado.Componer(tramo.Resumen));
            var primera = tramos.Min(tramo => tramo.Cobertura.PrimeraUtc);
            var ultima = tramos.Max(tramo => tramo.Cobertura.UltimaUtc);
            var hueco = tramos.Max(tramo => tramo.Cobertura.MayorHuecoSegundos);
            if (tramos.Length == 2 && tramos[0].Cobertura.UltimaUtc is { } fin && tramos[1].Cobertura.PrimeraUtc is { } inicio)
                hueco = Math.Max(hueco, (inicio - fin).TotalSeconds);
            var cobertura = CoberturaConsultaService.Calcular(resumen.Cantidad, primera, ultima, hueco,
                periodo.DesdeUtc, periodo.HastaUtc < instante ? periodo.HastaUtc : instante, umbralSegundos);
            if (tramos.Length != 2 || periodo.EnCurso) cobertura = cobertura with { Completa = false };
            var estados = tramos.SelectMany(tramo => tramo.Estados).GroupBy(estado => estado.Estado)
                .Select(estado => new ConteoEstadoObservado(estado.Key, estado.Sum(item => item.Cantidad),
                    estado.Min(item => item.PrimeraFechaUtc), estado.Max(item => item.UltimaFechaUtc))).ToArray();
            return new SeriePanelDto(grupo.Last().Panel, resumen, cobertura, estados, null);
        }).ToArray();
        return new(elementos, series, [], [], null);
    }
}
