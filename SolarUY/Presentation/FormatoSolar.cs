using System.Globalization;

namespace SolarUY.Presentation;

internal static class FormatoSolar
{
    private const string FormatoFecha = "dd/MM/yyyy HH:mm:ss";
    private static readonly CultureInfo _cultura = CultureInfo.GetCultureInfo("es-UY");
    private static readonly TimeZoneInfo _zonaUruguay = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public static string Vatios(double valor) =>
        valor is > 0 and < 0.01 ? "< 0,01" : valor.ToString("0.##", _cultura);

    public static string KilovatiosHora(decimal valor) => Magnitud(valor, 0.001m, "0.###");

    public static string Megavatios(decimal valor) => Magnitud(valor, 0.000001m, "0.######");

    public static string Coordenada(double valor) => valor.ToString("0.######", _cultura);

    public static string FechaUruguay(DateTime fechaUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, _zonaUruguay).ToString(FormatoFecha, _cultura);

    public static string FechaLocal(DateTimeOffset fecha) => fecha.ToString(FormatoFecha, _cultura);

    private static string Magnitud(decimal valor, decimal minimo, string formato) =>
        valor > 0 && valor < minimo ? "< " + minimo.ToString(formato, _cultura) : valor.ToString(formato, _cultura);
}
