using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using SolarUY.Application.Asistente;

namespace SolarUY.Presentation.Asistente;

public sealed class ServidorAsistenteMcp : IAsyncDisposable
{
    private readonly McpServer _servidor;
    private readonly CancellationTokenSource _cancelacion;
    private readonly Task _ejecucion;

    public ServidorAsistenteMcp(Stream entrada, Stream salida, IConsultaOperativaService consultas,
        DateTimeOffset instanteUtc, CancellationToken cancellationToken)
    {
        _cancelacion = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var descripciones = new[]
        {
            "Buscar parques, nodos o paneles por nombre y pertenencia. Devuelve candidatos paginados con ID y ruta. Pedir aclaración ante homónimos.",
            "Obtener características y pertenencia de un elemento inequívoco por Tipo e Id. Solo datos autorizados, sin configuración privada.",
            "Consultar última lectura hasta el instante del turno en Global, Parque, Nodo o Panel. Suma potencia vigente; separa antiguos y ausentes. Estado filtra el detalle paginado, no los totales.",
            "Consultar historial por Tipo e Id. Período UTC [desde,hasta), fechas locales Montevideo, estado y Cursor por fecha e ID. Indica continuación y cobertura de todo el período.",
            "Resumir todas las muestras por Agrupacion Panel, Nodo, Parque o Global. Potencia observada es suma de promedios por panel, nunca energía. Cada invocación admite hasta 31 días. ComponerConAnterior=true compone el siguiente tramo contiguo del mismo ámbito dentro de este turno. Pagina solo la salida.",
            "Comparar dos períodos con igual tramo hoy/ayer por defecto; DiasCompletos conserva días enteros. Diferencias, Orden y Cambio Aumento/Disminucion/Igualdad se calculan en SolarUY. Cobertura parcial no declara ganador."
        };
        var opcionesJson = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };
        var herramientas = CatalogoAsistente.Nombres.Select((nombre, indice) =>
            McpServerTool.Create(async (ConsultaOperativaDto consulta, CancellationToken cancelacion) =>
                JsonSerializer.Serialize(await consultas.EjecutarAsync(nombre, consulta, instanteUtc, cancelacion), opcionesJson),
                new McpServerToolCreateOptions
                {
                    Name = nombre, Description = descripciones[indice], ReadOnly = true, Destructive = false,
                    OpenWorld = false, Idempotent = true, SerializerOptions = opcionesJson
                })).ToArray();
        _servidor = McpServer.Create(new StreamServerTransport(entrada, salida), new McpServerOptions
        {
            ServerInfo = new() { Name = "SolarUY.Consultas", Version = "1.0.0" },
            ToolCollection = [.. herramientas]
        });
        _ejecucion = _servidor.RunAsync(_cancelacion.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _cancelacion.CancelAsync();
        try { await _ejecucion; }
        catch (OperationCanceledException) { }
        finally
        {
            await _servidor.DisposeAsync();
            _cancelacion.Dispose();
        }
    }
}
