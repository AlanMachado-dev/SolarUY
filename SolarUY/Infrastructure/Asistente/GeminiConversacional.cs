using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SolarUY.Application.Asistente;

namespace SolarUY.Infrastructure.Asistente;

public sealed class GeminiConversacional(HttpClient httpClient, AsistenteOptions opciones,
    ILogger<GeminiConversacional> logger) : IModeloConversacional
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AsistenteOptions _opciones = opciones;
    private readonly ILogger<GeminiConversacional> _logger = logger;

    public async Task<PasoModeloDto> CompletarAsync(TurnoModeloDto turno, CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        var correlacion = Guid.NewGuid().ToString("N");
        CodigoAsistente codigo = CodigoAsistente.FalloProveedor;
        try
        {
            if (!_opciones.Disponible) return new(CodigoAsistente.Deshabilitado, "El asistente no está configurado.", []);
            var contenido = new JsonArray();
            foreach (var mensaje in turno.Mensajes)
                contenido.Add(new JsonObject { ["role"] = mensaje.Rol == "assistant" ? "model" : "user",
                    // Application revalida las referencias por MCP y aporta sus datos mínimos en las instrucciones.
                    // El texto histórico se conserva sin apéndices que el modelo pueda imitar como respuesta visible.
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = mensaje.Texto }) });
            foreach (var intercambio in turno.Intercambios)
            {
                // Se conserva intacto content, incluidos thoughtSignature y IDs, únicamente durante este turno servidor.
                var original = JsonNode.Parse(intercambio.Paso.ProtocoloServidor ?? throw new JsonException()) ?? throw new JsonException();
                var idsProveedor = original["parts"]!.AsArray().Select(parte => parte?["functionCall"]?["id"]?.GetValue<string>())
                    .OfType<string>().ToHashSet();
                contenido.Add(original);
                var partes = new JsonArray();
                foreach (var resultado in intercambio.Resultados)
                {
                    var funcion = new JsonObject
                    {
                        ["name"] = resultado.Nombre,
                        ["response"] = JsonNode.Parse(resultado.ContenidoJson)
                    };
                    if (idsProveedor.Contains(resultado.Id)) funcion["id"] = resultado.Id;
                    partes.Add(new JsonObject { ["functionResponse"] = funcion });
                }
                contenido.Add(new JsonObject { ["role"] = "user", ["parts"] = partes });
            }
            var funciones = new JsonArray();
            foreach (var herramienta in turno.Herramientas)
                funciones.Add(new JsonObject { ["name"] = herramienta.Nombre, ["description"] = herramienta.Descripcion,
                    ["parametersJsonSchema"] = JsonNode.Parse(herramienta.EsquemaJson) });
            var cuerpo = new JsonObject
            {
                ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = turno.Instrucciones }) },
                ["contents"] = contenido,
                ["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = funciones }),
                ["toolConfig"] = new JsonObject { ["functionCallingConfig"] = new JsonObject { ["mode"] = "AUTO" } },
                ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = 4096 }
            };
            using var solicitud = new HttpRequestMessage(HttpMethod.Post,
                "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(_opciones.Modelo) + ":generateContent");
            solicitud.Headers.Add("x-goog-api-key", _opciones.ClaveApi);
            solicitud.Content = JsonContent.Create(cuerpo);
            using var respuesta = await _httpClient.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                codigo = respuesta.StatusCode == HttpStatusCode.TooManyRequests ? CodigoAsistente.CuotaAgotada : CodigoAsistente.FalloProveedor;
                var espera = respuesta.Headers.RetryAfter?.Delta?.TotalSeconds
                    ?? (respuesta.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds;
                return new(codigo, codigo == CodigoAsistente.CuotaAgotada
                    ? "La cuota del proveedor se agotó. Esperá antes de reintentar manualmente."
                    : "Gemini no está disponible o el modelo no está habilitado para esta cuenta. Reintentá manualmente.", [],
                    ReintentarEnSegundos: espera.HasValue ? (int)Math.Clamp(Math.Ceiling(espera.Value), 0, int.MaxValue) : null);
            }
            // No registrar el cuerpo, incluso en respuestas de error.
            await using var stream = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!documento.RootElement.TryGetProperty("candidates", out var candidatos) || candidatos.GetArrayLength() != 1)
                return new(CodigoAsistente.ModeloInvalido, "El proveedor bloqueó o no produjo una respuesta válida.", []);
            var candidato = candidatos[0];
            if (!candidato.TryGetProperty("finishReason", out var fin) || fin.GetString() != "STOP"
                || !candidato.TryGetProperty("content", out var contenidoModelo)
                || !contenidoModelo.TryGetProperty("parts", out var partesModelo))
                return new(CodigoAsistente.ModeloInvalido, "El proveedor devolvió una respuesta bloqueada o incompleta. Podés reintentar.", []);
            var textos = new List<string>();
            var llamadas = new List<LlamadaModeloDto>();
            foreach (var parte in partesModelo.EnumerateArray())
            {
                if (parte.TryGetProperty("functionCall", out var funcion))
                {
                    var nombre = funcion.GetProperty("name").GetString() ?? throw new JsonException();
                    var id = funcion.TryGetProperty("id", out var identificador) ? identificador.GetString() : null;
                    llamadas.Add(new(id ?? $"call_{llamadas.Count}", nombre, funcion.GetProperty("args").GetRawText()));
                }
                else if (parte.TryGetProperty("text", out var texto)
                    && (!parte.TryGetProperty("thought", out var pensamiento) || !pensamiento.GetBoolean()))
                    textos.Add(texto.GetString() ?? "");
            }
            if (llamadas.Select(llamada => llamada.Id).Distinct().Count() != llamadas.Count)
                throw new JsonException();
            if (llamadas.Count == 0 && textos.All(string.IsNullOrWhiteSpace)) throw new JsonException();
            codigo = CodigoAsistente.Exito;
            return new(codigo, string.Join("\n", textos), llamadas, contenidoModelo.GetRawText());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            codigo = CodigoAsistente.TiempoAgotado;
            return new(codigo, "Gemini demoró demasiado. Reintentá manualmente.", []);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            codigo = error is HttpRequestException ? CodigoAsistente.FalloProveedor : CodigoAsistente.ModeloInvalido;
            return new(codigo, "No se pudo obtener una respuesta válida de Gemini. Reintentá manualmente.", []);
        }
        finally
        {
            _logger.LogInformation("Asistente {Codigo}; DuracionMs={Duracion}; Mensajes={Cantidad}; Correlacion={Correlacion}",
                codigo, cronometro.ElapsedMilliseconds, turno.Mensajes.Count, correlacion);
        }
    }
}
