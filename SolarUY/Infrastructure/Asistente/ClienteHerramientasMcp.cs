using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SolarUY.Application.Asistente;

namespace SolarUY.Infrastructure.Asistente;

/// <summary>La raíz de composición proporciona streams y vida del servidor; no conoce Presentation.</summary>
public sealed class ClienteHerramientasMcp(
    Func<DateTimeOffset, CancellationToken, Task<(Stream Entrada, Stream Salida, IAsyncDisposable Servidor)>> abrirServidor)
    : IClienteHerramientasMcp
{
    private readonly Func<DateTimeOffset, CancellationToken, Task<(Stream Entrada, Stream Salida, IAsyncDisposable Servidor)>> _abrirServidor = abrirServidor;

    public async Task<IConexionHerramientasMcp> ConectarAsync(DateTimeOffset instanteUtc, CancellationToken cancellationToken)
    {
        (Stream Entrada, Stream Salida, IAsyncDisposable Servidor) recursos;
        try { recursos = await _abrirServidor(instanteUtc, cancellationToken); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new DependenciaAsistenteException(CodigoAsistente.FalloMcp);
        }
        try
        {
            var cliente = await McpClient.CreateAsync(new StreamClientTransport(recursos.Salida, recursos.Entrada),
                cancellationToken: cancellationToken);
            return new ConexionHerramientasMcp(cliente, recursos.Servidor);
        }
        catch
        {
            await recursos.Servidor.DisposeAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw new DependenciaAsistenteException(CodigoAsistente.FalloMcp);
        }
    }
}

public sealed class ConexionHerramientasMcp(McpClient cliente, IAsyncDisposable servidor) : IConexionHerramientasMcp
{
    private readonly McpClient _cliente = cliente;
    private readonly IAsyncDisposable _servidor = servidor;
    private IReadOnlyList<HerramientaModeloDto>? _catalogo;

    public async Task<IReadOnlyList<HerramientaModeloDto>> DescubrirAsync(CancellationToken cancellationToken)
    {
        try
        {
            var herramientas = await _cliente.ListToolsAsync(cancellationToken: cancellationToken);
            return _catalogo = herramientas.Select(herramienta => new HerramientaModeloDto(herramienta.Name,
                herramienta.Description ?? "", herramienta.JsonSchema.GetRawText())).ToArray();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new DependenciaAsistenteException(CodigoAsistente.FalloMcp);
        }
    }

    public async Task<ResultadoOperativoDto> InvocarAsync(LlamadaModeloDto llamada, CancellationToken cancellationToken)
    {
        if (!CatalogoAsistente.Nombres.Contains(llamada.Nombre) || _catalogo?.Any(herramienta => herramienta.Nombre == llamada.Nombre) != true)
            return new(CodigoAsistente.ArgumentosInvalidos, DateTimeOffset.UtcNow, Mensaje: "Herramienta fuera de catálogo.");
        try
        {
            using var documento = JsonDocument.Parse(llamada.ArgumentosJson);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || raiz.EnumerateObject().Count() != 1
                || !raiz.TryGetProperty("consulta", out var consulta) || consulta.ValueKind != JsonValueKind.Object)
                return new(CodigoAsistente.ArgumentosInvalidos, DateTimeOffset.UtcNow, Mensaje: "Se requiere únicamente el objeto consulta.");
            var opciones = new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
                PropertyNameCaseInsensitive = true, RespectRequiredConstructorParameters = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
            _ = consulta.Deserialize<ConsultaOperativaDto>(opciones) ?? throw new JsonException();
            var argumentos = new Dictionary<string, object?> { ["consulta"] = consulta.Clone() };
            var resultado = await _cliente.CallToolAsync(llamada.Nombre, argumentos, cancellationToken: cancellationToken);
            if (resultado.IsError == true) return new(CodigoAsistente.FalloMcp, DateTimeOffset.UtcNow, Mensaje: "La herramienta MCP no pudo ejecutarse.");
            var texto = resultado.Content.OfType<TextContentBlock>().SingleOrDefault()?.Text;
            try
            {
                return JsonSerializer.Deserialize<ResultadoOperativoDto>(texto ?? "", opciones) ?? throw new JsonException();
            }
            catch (JsonException)
            {
                return new(CodigoAsistente.FalloMcp, DateTimeOffset.UtcNow, Mensaje: "MCP devolvió un resultado inválido, no equivale a ausencia de datos.");
            }
        }
        catch (JsonException)
        {
            return new(CodigoAsistente.ArgumentosInvalidos, DateTimeOffset.UtcNow, Mensaje: "Argumentos o respuesta fuera del contrato MCP.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(CodigoAsistente.FalloMcp, DateTimeOffset.UtcNow, Mensaje: "Falló la conexión MCP. Reintentá manualmente.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _cliente.DisposeAsync(); }
        finally { await _servidor.DisposeAsync(); }
    }
}
