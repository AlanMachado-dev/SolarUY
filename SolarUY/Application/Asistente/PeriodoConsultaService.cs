using System.Globalization;

namespace SolarUY.Application.Asistente;

public sealed class PeriodoConsultaService(TimeProvider reloj, AsistenteOptions opciones)
{
    private readonly TimeProvider _reloj = reloj;
    private readonly AsistenteOptions _opciones = opciones;
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public DateTimeOffset Ahora => _reloj.GetUtcNow();

    public PeriodoConsultaDto Resolver(string periodo, string? desde, string? hasta,
        DateTimeOffset instanteUtc, bool comparar = false, bool diasCompletos = false,
        double? cantidadPeriodo = null, string? unidadPeriodo = null)
    {
        if (periodo == "Ultimos")
        {
            if (desde is not null || hasta is not null || diasCompletos)
                throw new ArgumentException("Para los últimos períodos indicá solo cantidad y unidad, sin fechas ni días completos.");
            return ResolverUltimos(cantidadPeriodo, unidadPeriodo, instanteUtc);
        }
        if (cantidadPeriodo.HasValue || unidadPeriodo is not null)
            throw new ArgumentException("La cantidad y la unidad solo se admiten para un período de tipo Ultimos.");
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
                if (fin <= inicio) throw new ArgumentException("El período de esta mañana todavía no comenzó; empieza a las 06:00 en Uruguay.");
                break;
            case "Explicito":
                if (desde is null || hasta is null) throw new ArgumentException("Indicá desde y hasta en formato ISO. Sin zona se interpreta America/Montevideo.");
                return Crear(InterpretarFecha(desde), InterpretarFecha(hasta), instanteUtc, periodo);
            default: throw new ArgumentException("No pude determinar el período. Indicá hoy, ayer, esta mañana, una duración reciente o fechas de inicio y fin.");
        }
        return Crear(ConvertirUtc(inicio), ConvertirUtc(fin), instanteUtc,
            comparar && !diasCompletos ? periodo + ": igual tramo transcurrido" : periodo);
    }

    private PeriodoConsultaDto ResolverUltimos(double? cantidad, string? unidad, DateTimeOffset instante)
    {
        if (cantidad is not { } valor || !double.IsFinite(valor) || valor <= 0)
            throw new ArgumentException("Indicá una cantidad positiva para el período reciente.");
        var segundosPorUnidad = unidad switch
        {
            "Segundos" => 1, "Minutos" => 60, "Horas" => 3600, "Dias" => 86400,
            _ => throw new ArgumentException("La duración debe expresarse en segundos, minutos, horas o días.")
        };
        var segundos = valor * segundosPorUnidad;
        if (!double.IsFinite(segundos) || segundos > TimeSpan.FromDays(_opciones.MaximoDias).TotalSeconds)
            throw new ArgumentException($"Consultá hasta {_opciones.MaximoDias} días por tramo, sin recortar el período solicitado.");
        // Validar antes de crear TimeSpan o restar evita desbordamientos y duraciones sub-tick vacías.
        var duracion = TimeSpan.FromSeconds(segundos);
        var hasta = instante.ToUniversalTime();
        if (duracion <= TimeSpan.Zero || duracion.Ticks > hasta.Ticks)
            throw new ArgumentException("La duración solicitada está fuera del intervalo de fechas admitido.");
        var nombreUnidad = unidad switch
        {
            "Segundos" => valor == 1 ? "segundo" : "segundos",
            "Minutos" => valor == 1 ? "minuto" : "minutos",
            "Horas" => valor == 1 ? "hora" : "horas",
            _ => valor == 1 ? "día" : "días"
        };
        var prefijo = unidad == "Horas" ? valor == 1 ? "última" : "últimas" : valor == 1 ? "último" : "últimos";
        var descripcion = $"{prefijo} {valor.ToString("0.################", CultureInfo.GetCultureInfo("es-UY"))} {nombreUnidad}";
        return Crear(hasta - duracion, hasta, instante, descripcion);
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
        if (hasta <= desde) throw new ArgumentException("La fecha de fin debe ser posterior a la fecha de inicio; el período no puede estar vacío.");
        if (hasta - desde > TimeSpan.FromDays(_opciones.MaximoDias))
            throw new ArgumentException($"El máximo es {_opciones.MaximoDias} días por llamada. Dividí el período; componé los acumulados por panel ponderando cantidades y evaluá los huecos entre tramos.");
        return new(desde, hasta, TimeZoneInfo.ConvertTime(desde, _zona), TimeZoneInfo.ConvertTime(hasta, _zona),
            _zona.Id, hasta > instante, descripcion);
    }
}
