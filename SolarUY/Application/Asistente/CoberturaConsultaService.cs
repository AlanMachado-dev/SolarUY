namespace SolarUY.Application.Asistente;

public static class CoberturaConsultaService
{
    public static CoberturaPanelDto Calcular(long cantidad, DateTimeOffset? primera, DateTimeOffset? ultima,
        double huecoInternoSegundos, DateTimeOffset desde, DateTimeOffset hasta, int umbralSegundos)
    {
        var hueco = primera.HasValue && ultima.HasValue ? Math.Max(huecoInternoSegundos,
            Math.Max((primera.Value - desde).TotalSeconds, (hasta - ultima.Value).TotalSeconds)) : (hasta - desde).TotalSeconds;
        return new(cantidad, primera, ultima, hueco, cantidad > 0 && hueco <= umbralSegundos);
    }
}
