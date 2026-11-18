using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SolarUY.Application.Asistente;

/// <summary>Prepara texto visible sin alterar los datos usados para consultas y cálculos.</summary>
public sealed class FormatoRespuestaAsistenteService
{
    private readonly CultureInfo _cultura = CultureInfo.GetCultureInfo("es-UY");
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
    private readonly JsonSerializerOptions _serializacion = new() { Converters = { new JsonStringEnumConverter() } };

    public ResultadoOperativoDto Preparar(ResultadoOperativoDto resultado)
    {
        var valores = new List<ValorPresentacionDto>();
        if (resultado.Datos is not null)
            PrepararValores(JsonSerializer.SerializeToElement(resultado.Datos, _serializacion), "Datos", valores);
        return resultado with { Presentacion = new(Fecha(resultado.InstanteUtc),
            Periodo(resultado.Periodo), Periodo(resultado.Referencia), valores) };
    }

    public string Potencia(decimal? valor, bool vigente = false)
    {
        if (valor is null) return vigente ? "No hay mediciones recientes" : "No hay mediciones disponibles";
        var redondeado = decimal.Round(valor.Value, 2, MidpointRounding.AwayFromZero);
        if (valor > 0 && redondeado == 0) return "menos de 0,01 W";
        if (valor < 0 && redondeado == 0) return "entre -0,01 y 0 W";
        return redondeado.ToString("0.##", _cultura) + " W";
    }

    public string Fecha(DateTimeOffset instante) =>
        TimeZoneInfo.ConvertTime(instante, _zona).ToString("dd/MM/yyyy HH:mm:ss", _cultura) + " (hora de Uruguay)";

    public string? Periodo(PeriodoConsultaDto? periodo)
    {
        if (periodo is null) return null;
        var desde = TimeZoneInfo.ConvertTime(periodo.DesdeUtc, _zona);
        var hasta = TimeZoneInfo.ConvertTime(periodo.HastaUtc, _zona);
        var fin = hasta.ToString(desde.Date == hasta.Date ? "HH:mm:ss" : "dd/MM/yyyy HH:mm:ss", _cultura);
        var descripcion = periodo.Descripcion switch
        {
            "Hoy" => "hoy", "Ayer" => "ayer", "Manana" => "esta mañana", "Explicito" => "período solicitado",
            "Hoy: igual tramo transcurrido" => "hoy, tramo transcurrido",
            "Ayer: igual tramo transcurrido" => "ayer, mismo tramo horario",
            _ when periodo.Descripcion.StartsWith("últim", StringComparison.Ordinal) => periodo.Descripcion,
            _ => "período consultado"
        };
        return $"{descripcion}: del {desde.ToString("dd/MM/yyyy HH:mm:ss", _cultura)} al {fin} (hora de Uruguay)";
    }

    private void PrepararValores(JsonElement dato, string ruta, List<ValorPresentacionDto> valores)
    {
        if (dato.ValueKind == JsonValueKind.Array)
        {
            var indice = 0;
            foreach (var item in dato.EnumerateArray()) PrepararValores(item, $"{ruta}[{indice++}]", valores);
            return;
        }
        if (dato.ValueKind != JsonValueKind.Object) return;
        foreach (var propiedad in dato.EnumerateObject())
        {
            var campo = ruta + "." + propiedad.Name;
            var texto = PrepararValor(propiedad.Name, propiedad.Value);
            if (texto is not null) valores.Add(new(campo, texto));
            else PrepararValores(propiedad.Value, campo, valores);
        }
    }

    private string? PrepararValor(string campo, JsonElement valor)
    {
        // SumaW es un acumulado interno; nunca se prepara como potencia o energía visible.
        if (campo.EndsWith('W') && campo != "SumaW")
        {
            if (valor.ValueKind == JsonValueKind.Null) return Potencia(null, campo == "PotenciaVigenteW");
            if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var potencia)) return Potencia(potencia);
        }
        if (campo.EndsWith("Utc", StringComparison.Ordinal) && valor.ValueKind == JsonValueKind.String
            && valor.TryGetDateTimeOffset(out var fecha)) return Fecha(fecha);
        if (campo is "EsParcial" or "Completa" && valor.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var parcial = campo == "EsParcial" ? valor.GetBoolean() : !valor.GetBoolean();
            return parcial ? "Faltan registros de parte del período" : "Cobertura observada completa; no garantiza monitoreo continuo";
        }
        if (valor.ValueKind != JsonValueKind.String || campo is not ("Estado" or "Vigencia" or "Conclusion")) return null;
        return valor.GetString() switch
        {
            "Operativo" => "operativo", "EnFalla" => "en falla", "EnMantenimiento" => "en mantenimiento",
            "Desactivado" => "desactivado", "SinMedicion" => "sin mediciones", "Antigua" => "sin mediciones recientes",
            "Vigente" => "medición reciente", "NoConcluyente" => "Los datos no permiten una conclusión global",
            "Aumento" => "aumento observado", "Disminucion" => "disminución observada", "Igualdad" => "sin diferencia observada",
            _ => null
        };
    }

    public string NormalizarTexto(string texto, IReadOnlyCollection<string> literales)
    {
        // Solo quitar delimitadores reconocidos, nunca signos que sean parte de un nombre conocido.
        var limpio = Regex.Replace(texto, @"(?m)^(\s*)\\([-*])(?= )", coincidencia =>
            EstaProtegido(texto, coincidencia.Index + coincidencia.Groups[1].Length, 2, literales)
                ? coincidencia.Value : coincidencia.Groups[1].Value + "•");
        return Regex.Replace(limpio, @"(\*\*|__)(?=\S)([^\r\n]+?)\1", coincidencia =>
            EstaProtegido(limpio, coincidencia.Index, 2, literales)
                || EstaProtegido(limpio, coincidencia.Index + coincidencia.Length - 2, 2, literales)
                ? coincidencia.Value : coincidencia.Groups[2].Value);
    }

    private static bool EstaProtegido(string texto, int inicio, int longitud, IReadOnlyCollection<string> literales)
    {
        foreach (var literal in literales.Where(literal => !string.IsNullOrEmpty(literal)))
        {
            var posicion = texto.IndexOf(literal, StringComparison.Ordinal);
            while (posicion >= 0)
            {
                if (posicion < inicio + longitud && posicion + literal.Length > inicio) return true;
                posicion = texto.IndexOf(literal, posicion + 1, StringComparison.Ordinal);
            }
        }
        return false;
    }
}
