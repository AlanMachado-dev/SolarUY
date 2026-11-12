using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Energia;

public sealed class EnergiaService(IEnergiaRepository repository, IUsuarioActualService usuarioActual, TimeProvider reloj)
{
    private static readonly TimeZoneInfo _zonaUruguay = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public async Task<EnergiaEstimadaDto?> ConsultarHoyAsync(TipoComponenteEnergia tipo, Guid componenteId,
        CancellationToken cancellationToken = default)
    {
        if (!usuarioActual.EstaAutenticado ||
            (!usuarioActual.PerteneceAlRol("Administrador") && !usuarioActual.PerteneceAlRol("Operador")))
            return null;

        var corteUtc = reloj.GetUtcNow();
        var corteLocal = TimeZoneInfo.ConvertTime(corteUtc, _zonaUruguay);
        var inicioLocal = new DateTimeOffset(corteLocal.Date, _zonaUruguay.GetUtcOffset(corteLocal.Date));
        var inicioUtc = inicioLocal.ToUniversalTime();
        var lecturas = await repository.ObtenerLecturasPorPanelAsync(tipo, componenteId,
            inicioUtc - EnergiaEstimadaPanel.MaximaSeparacionEntreLecturas, corteUtc, cancellationToken);

        if (lecturas.Count == 0 && tipo == TipoComponenteEnergia.Panel)
            return null;

        return Resumir(lecturas, inicioLocal, inicioUtc, corteLocal, corteUtc);
    }

    private static EnergiaEstimadaDto Resumir(IReadOnlyDictionary<Guid, IReadOnlyList<LecturaEnergia>> lecturas,
        DateTimeOffset inicioLocal, DateTimeOffset inicioUtc, DateTimeOffset corteLocal, DateTimeOffset corteUtc)
    {
        decimal totalKwh = 0;
        var conDatos = 0;
        var parcial = false;

        foreach (var serie in lecturas.Values)
        {
            var estimacion = EnergiaEstimadaPanel.Calcular(serie, inicioUtc, corteUtc);
            if (estimacion.EnergiaKwh is not { } energiaKwh)
                continue;

            totalKwh += energiaKwh;
            conDatos++;
            parcial |= estimacion.EsParcial;
        }

        var cobertura = (lecturas.Count, conDatos) switch
        {
            (0, _) => CoberturaEnergia.SinPaneles,
            (_, 0) => CoberturaEnergia.SinDatosSuficientes,
            (var esperados, var observados) when parcial || esperados != observados => CoberturaEnergia.Parcial,
            _ => CoberturaEnergia.Completa
        };

        return new EnergiaEstimadaDto(conDatos > 0 ? totalKwh : null, cobertura,
            inicioLocal, corteLocal, lecturas.Count, conDatos);
    }
}
