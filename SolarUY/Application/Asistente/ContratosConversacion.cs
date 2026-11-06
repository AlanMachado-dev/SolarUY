namespace SolarUY.Application.Asistente;

public sealed record SesionAsistenteDto(string Marca, DateTimeOffset EmisionUtc, DateTimeOffset VencimientoUtc);
public sealed record MensajeAsistenteDto(string Rol, string Texto, ReferenciaElementoDto[]? Referencias = null);
public sealed record PreguntaAsistenteDto(string Pregunta, string MarcaSesion,
    MensajeAsistenteDto[] Historial, ReferenciaElementoDto? ContextoPagina, bool HistorialRecortado = false);
public sealed record RespuestaAsistenteDto(CodigoAsistente Codigo, string Mensaje,
    ReferenciaElementoDto[]? Referencias = null, int? ReintentarEnSegundos = null, string? Correlacion = null);
public sealed record HerramientaModeloDto(string Nombre, string Descripcion, string EsquemaJson);
public sealed record LlamadaModeloDto(string Id, string Nombre, string ArgumentosJson);
public sealed record ResultadoHerramientaDto(string Id, string Nombre, string ContenidoJson);
public sealed record PasoModeloDto(CodigoAsistente Codigo, string? Texto,
    IReadOnlyList<LlamadaModeloDto> Llamadas, string? ProtocoloServidor = null, int? ReintentarEnSegundos = null);
public sealed record IntercambioModeloDto(PasoModeloDto Paso, IReadOnlyList<ResultadoHerramientaDto> Resultados);
public sealed record TurnoModeloDto(string Instrucciones, IReadOnlyList<MensajeAsistenteDto> Mensajes,
    IReadOnlyList<HerramientaModeloDto> Herramientas, IReadOnlyList<IntercambioModeloDto> Intercambios);

public interface IModeloConversacional
{
    Task<PasoModeloDto> CompletarAsync(TurnoModeloDto turno, CancellationToken cancellationToken);
}

public interface IConexionHerramientasMcp : IAsyncDisposable
{
    Task<IReadOnlyList<HerramientaModeloDto>> DescubrirAsync(CancellationToken cancellationToken);
    Task<ResultadoOperativoDto> InvocarAsync(LlamadaModeloDto llamada, CancellationToken cancellationToken);
}

public interface IClienteHerramientasMcp
{
    Task<IConexionHerramientasMcp> ConectarAsync(DateTimeOffset instanteUtc, CancellationToken cancellationToken);
}

public interface IAsistenteService
{
    Task<RespuestaAsistenteDto> PreguntarAsync(PreguntaAsistenteDto pregunta, CancellationToken cancellationToken);
}

public sealed class DependenciaAsistenteException(CodigoAsistente codigo) : Exception
{
    public CodigoAsistente Codigo { get; } = codigo;
}

public static class CatalogoAsistente
{
    public static IReadOnlyList<string> Nombres { get; } = Array.AsReadOnly(new[]
    {
        "BuscarElementos", "ObtenerDetalle", "ConsultarEstadoActual", "ConsultarHistorial",
        "ResumirMediciones", "CompararMediciones"
    });
}
