using SolarUY.Domain.Paneles;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SolarUY.Application.Asistente;

[JsonConverter(typeof(JsonStringEnumConverter<CodigoAsistente>))]
public enum CodigoAsistente
{
    Exito, SinDatos, NoEncontrado, AccesoDenegado, ArgumentosInvalidos, LimiteAlcanzado,
    FalloPersistencia, FalloMcp, FalloProveedor, CuotaAgotada, TiempoAgotado, Cancelado,
    ModeloInvalido, Deshabilitado, AclaracionNecesaria
}

public sealed record ReferenciaElementoDto(string Tipo, Guid Id);
public sealed record ElementoOperativoDto(string Tipo, Guid Id, string Nombre,
    Guid ParqueId, string Parque, Guid? NodoId = null, string? Nodo = null,
    string? Modelo = null, float? PotenciaNominalW = null, DateTime? FechaInstalacion = null,
    string? Ubicacion = null, float? Latitud = null, float? Longitud = null)
{
    public ElementoOperativoDto() : this("", Guid.Empty, "", Guid.Empty, "") { }
}

public sealed record PeriodoConsultaDto(DateTimeOffset DesdeUtc, DateTimeOffset HastaUtc,
    DateTimeOffset DesdeLocal, DateTimeOffset HastaLocal, string Zona, bool EnCurso, string Descripcion);
public sealed record CursorMedicionDto(DateTimeOffset FechaUtc, Guid Id);

/// <summary>Contrato cerrado de argumentos. Los ámbitos Global no están limitados a la página de elementos.</summary>
public sealed record ConsultaOperativaDto
{
    [Description("Tipo: Global, Parque, Nodo o Panel. Global abarca todos los elementos autorizados.")]
    public string Tipo { get; init; } = "Global";
    [Description("ID inequívoco del elemento. No usar nombres como identificadores.")]
    public Guid? Id { get; init; }
    [Description("Hasta 100 IDs del mismo tipo. No combinar con Id ni Tipo Global.")]
    public Guid[] Ids { get; init; } = [];
    [Description("Solo BuscarElementos. Los homónimos requieren aclaración antes de seleccionar un ID.")]
    public string? Nombre { get; init; }
    [Description("Limitar al parque padre por ID.")]
    public Guid? ParqueId { get; init; }
    [Description("Limitar al nodo padre por ID, validando pertenencia.")]
    public Guid? NodoId { get; init; }
    [Description("Página de salida desde 1. No limita muestras usadas en agregados.")]
    public int Pagina { get; init; } = 1;
    [Description("50 resultados por defecto; máximo 200.")]
    public int? TamanoPagina { get; init; }
    [Description("Operativo, EnFalla, EnMantenimiento o Desactivado. En estado actual filtra solo el detalle.")]
    public string? Estado { get; init; }
    [Description("Panel, Nodo, Parque o Global. Global resume todo el ámbito sin enumerar paneles.")]
    public string Agrupacion { get; init; } = "Panel";
    [Description("Hoy, Ayer, Manana (06:00–12:00 locales) o Explicito. Zona America/Montevideo.")]
    public string Periodo { get; init; } = "Hoy";
    [Description("Inicio inclusivo ISO. Sin offset se interpreta America/Montevideo.")]
    public string? Desde { get; init; }
    [Description("Fin exclusivo ISO. Máximo 31 días nuevos por invocación.")]
    public string? Hasta { get; init; }
    [Description("Período de referencia: Ayer por defecto, Hoy, Manana o Explicito.")]
    public string PeriodoReferencia { get; init; } = "Ayer";
    [Description("Inicio inclusivo ISO del período de referencia.")]
    public string? DesdeReferencia { get; init; }
    [Description("Fin exclusivo ISO del período de referencia.")]
    public string? HastaReferencia { get; init; }
    [Description("Solo para días completos explícitos. Por defecto hoy/ayer usa igual tramo transcurrido.")]
    public bool DiasCompletos { get; init; }
    [Description("ResumirMediciones: componer con el tramo anterior contiguo del mismo ámbito/filtro/agrupación dentro del turno actual.")]
    public bool ComponerConAnterior { get; init; }
    [Description("Orden de diferencias Ascendente o Descendente, calculado en SolarUY.")]
    public string Orden { get; init; } = "Ascendente";
    [Description("Filtrar CambioObservado: Aumento, Disminucion o Igualdad. Incluye diferencias parciales, pero su Conclusion sigue NoConcluyente; nunca declarar ganador con cobertura parcial.")]
    public string? Cambio { get; init; }
    [Description("Cursor de historial recibido anteriormente: fecha UTC e ID.")]
    public CursorMedicionDto? Cursor { get; init; }
}

public sealed record MuestraOperativaDto(Guid Id, Guid PanelId, DateTimeOffset FechaUtc,
    decimal PotenciaW, EstadoPanel Estado);
public sealed record CoberturaPanelDto(long Cantidad, DateTimeOffset? PrimeraUtc,
    DateTimeOffset? UltimaUtc, double MayorHuecoSegundos, bool Completa);
public sealed record SeriePanelDto(ElementoOperativoDto Panel, ResumenMediciones Resumen,
    CoberturaPanelDto Cobertura, IReadOnlyList<ConteoEstadoObservado> Estados, MuestraOperativaDto? Ultima);
public sealed record InstantaneaConsultaDto(IReadOnlyList<ElementoOperativoDto> Elementos,
    IReadOnlyList<SeriePanelDto> Series, IReadOnlyList<SeriePanelDto> Referencia,
    IReadOnlyList<MuestraOperativaDto> Muestras, CursorMedicionDto? Continuacion);
public sealed record PaginaElementosDto(IReadOnlyList<ElementoOperativoDto> Elementos, int Total);
public sealed record ResumenElementoDto(ElementoOperativoDto Elemento, PotenciaObservada Potencia,
    ResumenMediciones Muestras, IReadOnlyList<SeriePanelDto> Paneles,
    IReadOnlyList<ConteoEstadoObservado> Estados);
public sealed record ComparacionElementoDto(ElementoOperativoDto Elemento, PotenciaObservada Actual,
    PotenciaObservada Referencia, ComparacionPotencia Comparacion);
public sealed record EstadoPanelConsultaDto(ElementoOperativoDto Panel, MuestraOperativaDto? Ultima, string Vigencia);
public sealed record EstadoConsultaDto(EstadoActualObservado Total, IReadOnlyList<EstadoPanelConsultaDto> Detalle, int TotalDetalle);
public sealed record ResultadoOperativoDto(CodigoAsistente Codigo, DateTimeOffset InstanteUtc,
    object? Datos = null, string? Mensaje = null, int? Total = null, int? PaginaSiguiente = null,
    CursorMedicionDto? Cursor = null, PeriodoConsultaDto? Periodo = null,
    PeriodoConsultaDto? Referencia = null)
{
    public string Unidad => "W";
    public string Origen => "Simulado: el sensor envía cada 20 segundos mientras su pantalla está activa.";
    public string Advertencia => "Cobertura observada, no garantiza monitoreo continuo. Herramientas diferentes pueden observar nuevas escrituras. No permite determinar causas de falla.";
}

public interface IConsultaOperativaRepository
{
    Task<PaginaElementosDto> BuscarAsync(ConsultaOperativaDto consulta, CancellationToken cancellationToken);
    Task<InstantaneaConsultaDto> LeerAsync(ConsultaOperativaDto consulta, DateTimeOffset instanteUtc,
        PeriodoConsultaDto? periodo, PeriodoConsultaDto? referencia, bool historial,
        CancellationToken cancellationToken);
}

public interface IConsultaOperativaService
{
    Task<ResultadoOperativoDto> EjecutarAsync(string herramienta, ConsultaOperativaDto consulta,
        DateTimeOffset instanteUtc, CancellationToken cancellationToken);
}

public sealed class AsistenteOptions
{
    public bool Habilitado { get; set; }
    public string Modelo { get; set; } = "gemini-3.5-flash-lite";
    public string ClaveApi { get; set; } = "";
    public int ResultadosPagina { get; set; } = 50;
    public int MaximoPagina { get; set; } = 200;
    public int MaximoIds { get; set; } = 100;
    public int MaximoDias { get; set; } = 31;
    public int VigenciaSegundos { get; set; } = 60;
    public int HuecoSegundos { get; set; } = 60;
    public int MaximoPregunta { get; set; } = 2000;
    public int MaximoMensajes { get; set; } = 30;
    public int MaximoContexto { get; set; } = 32000;
    public int MaximoRondas { get; set; } = 8;
    public int MaximoInvocaciones { get; set; } = 12;
    public int TiempoTurnoSegundos { get; set; } = 60;
    public bool Disponible => Habilitado && !string.IsNullOrWhiteSpace(ClaveApi) && !string.IsNullOrWhiteSpace(Modelo);
}
