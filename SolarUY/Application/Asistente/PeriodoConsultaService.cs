using System.Globalization;

namespace SolarUY.Application.Asistente;

public sealed class PeriodoConsultaService(TimeProvider reloj, AsistenteOptions opciones)
{
    private readonly TimeProvider _reloj = reloj;
    private readonly AsistenteOptions _opciones = opciones;
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public DateTimeOffset Ahora => _reloj.GetUtcNow();

    public PeriodoConsultaDto Resolver(string periodo, string? desde, string? hasta,
        DateTimeOffset instanteUtc, bool comparar = false, bool diasCompletos = false)
    {
        var ahoraLocal = TimeZoneInfo.ConvertTime(instanteUtc, _zona);
        var hoy = ahoraLocal.Date;
        var inicio = hoy;
        var fin = ahoraLocal.DateTime;
        switch (periodo)
        {
            case "Hoy": fin = diasCompletos ? hoy.AddDays(1) : fin; break;
            case "Ayer":
                inicio = hoy.AddDays(-1);
                fin = comparar && !diasCompletos ? inicio + ahoraLocal.TimeOfDay : hoy;
                break;
            case "Manana":
                inicio = hoy.AddHours(6);
                fin = hoy.AddHours(12);
                if (fin > ahoraLocal.DateTime) fin = ahoraLocal.DateTime;
                if (fin < inicio) fin = inicio;
                break;
            case "Explicito":
                if (desde is null || hasta is null) throw new ArgumentException("Indicá desde y hasta en formato ISO. Sin zona se interpreta America/Montevideo.");
                return Crear(InterpretarFecha(desde), InterpretarFecha(hasta), instanteUtc, periodo);
            default: throw new ArgumentException("Período ambiguo. Usá Hoy, Ayer, Manana (06:00–12:00) o Explicito.");
        }
        return Crear(ConvertirUtc(inicio), ConvertirUtc(fin), instanteUtc,
            comparar && !diasCompletos ? periodo + ": igual tramo transcurrido" : periodo);
    }

    private DateTimeOffset ConvertirUtc(DateTime local) => new(TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zona));

    private DateTimeOffset InterpretarFecha(string fecha)
    {
        if (fecha.Length is < 10 or > 40 || fecha[4] != '-' || fecha[7] != '-')
            throw new ArgumentException("Usá fechas ISO yyyy-MM-dd o yyyy-MM-ddTHH:mm:ss con zona opcional.");
        var tieneZona = fecha.EndsWith('Z') || fecha.Length >= 16 &&
            fecha[^6] is '+' or '-' && fecha[^3] == ':';
        if (tieneZona && DateTimeOffset.TryParse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var absoluta))
            return absoluta.ToUniversalTime();
        if (!tieneZona && DateTime.TryParse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return ConvertirUtc(local);
        throw new ArgumentException("La fecha indicada no es válida.");
    }

    private PeriodoConsultaDto Crear(DateTimeOffset desde, DateTimeOffset hasta, DateTimeOffset instante, string descripcion)
    {
        if (hasta < desde) throw new ArgumentException("El límite hasta debe ser posterior a desde.");
        if (hasta - desde > TimeSpan.FromDays(_opciones.MaximoDias))
            throw new ArgumentException($"El máximo es {_opciones.MaximoDias} días por llamada. Dividí el período; componé los acumulados por panel ponderando cantidades y evaluá los huecos entre tramos.");
        return new(desde, hasta, TimeZoneInfo.ConvertTime(desde, _zona), TimeZoneInfo.ConvertTime(hasta, _zona),
            _zona.Id, hasta > instante, descripcion);
    }
}
