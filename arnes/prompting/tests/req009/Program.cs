using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Asistente;

if (args.Contains("--verificar-guardas-mcp"))
{
    await GuardasMcpService.EjecutarAsync();
    return;
}

if (args.Contains("--web-aceptacion"))
{
    await ServidorFuncional.EjecutarAsync(PreparacionEscenariosService.ObtenerConexion(), certificadoAceptacion: true);
    return;
}

if (args.Contains("--preparar-escenarios") || args.Contains("--actualizar-escenarios"))
{
    try { await PreparacionEscenariosService.EjecutarAsync(args.Contains("--actualizar-escenarios")); }
    catch (Exception error)
    {
        Console.Error.WriteLine("Escenarios aislados: fallo depurado " + error.GetType().Name);
        Environment.ExitCode = 1;
    }
    return;
}

if (args.Contains("--exportar-catalogo"))
{
    await ExportacionCatalogoService.ExportarAsync();
    return;
}

if (args.Contains("--preparar-despliegue") || args.Contains("--verificar-despliegue") || args.Contains("--verificar-guardas") || args.Contains("--verificar-regresion") || args.Contains("--verificar-operaciones-fallo"))
{
    try { await VerificacionDespliegue.EjecutarAsync(args.Contains("--preparar-despliegue"), args.Contains("--verificar-guardas"),
        args.Contains("--verificar-regresion") || args.Contains("--verificar-operaciones-fallo"), !args.Contains("--verificar-operaciones-fallo")); }
    catch (Exception error)
    {
        Console.Error.WriteLine("Despliegue aislado: fallo depurado " + error.GetType().Name);
        Environment.ExitCode = 1;
    }
    return;
}

// Destino fijo, desechable y separado de la configuración habitual. No lee appsettings.Local.json.
const string conexion = "server=127.0.0.1;port=43319;database=solaruy_req009;user=root;password=;OldGuids=true";
var destino = new MySqlConnectionStringBuilder(conexion);
Comprobar(destino.Server == "127.0.0.1" && destino.Port == 43319 && destino.Database == "solaruy_req009", "Destino aislado validado");
if (args.Contains("--web")) { await ServidorFuncional.EjecutarAsync(conexion); return; }
var opcionesDb = new DbContextOptionsBuilder<SolarUyDbContext>();
PersistenciaConfiguration.ConfigurarContexto(opcionesDb, conexion);
var factory = new ContextoFactory(opcionesDb.Options);
var instante = new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);
var reloj = new RelojFijo(instante);
var opciones = new AsistenteOptions { Habilitado = true, ClaveApi = "doble-funcional-no-credencial" };
var usuario = new UsuarioFuncional(instante);
var periodos = new PeriodoConsultaService(reloj, opciones);
var repository = new ConsultaOperativaRepository(factory, opciones);
var consultas = new ConsultaOperativaService(repository, usuario, periodos, opciones);
var nodos = new List<Nodo>();
var paneles = new List<Panel>();
await using (var contexto = factory.CreateDbContext())
{
    if (args.Contains("--reiniciar")) await contexto.Database.EnsureDeletedAsync();
    await contexto.Database.EnsureCreatedAsync();
    Comprobar(await contexto.ParquesSolares.CountAsync() == 0, "Base aislada vacía; no se reutilizan datos previos");
    foreach (var nombreParque in new[] { "Norte", "Sur" })
    {
        var parque = ParqueSolar.Crear(nombreParque, "Datos simulados req009");
        for (var i = 0; i < 2; i++)
        {
            var nodo = parque.AgregarNodo("Nodo " + i, 0, 0,
                Inversor.Crear("Inversor", Guid.NewGuid().ToString(), Sensor.Crear("Sensor", Guid.NewGuid().ToString())));
            nodos.Add(nodo);
            for (var j = 0; j < 30; j++) paneles.Add(nodo.AgregarPanel("Panel " + j, "Simulado", 500, instante.UtcDateTime.AddDays(-90)));
        }
        contexto.Add(parque);
    }
    foreach (var panel in paneles.Take(118))
    {
        for (var i = 0; i < 90; i++)
            contexto.Add(Medicion.Crear(panel, instante.UtcDateTime.AddSeconds(-20 * i), panel == paneles[0] ? 0 : 100, (EstadoPanel)(i % 4)));
        for (var i = 0; i < 30; i++)
            contexto.Add(Medicion.Crear(panel, instante.UtcDateTime.AddDays(-1).AddSeconds(-20 * i), 200, EstadoPanel.Operativo));
    }
    contexto.Add(Medicion.Crear(paneles[118], instante.UtcDateTime.AddMinutes(-5), 40, EstadoPanel.EnFalla));
    await contexto.SaveChangesAsync();
}

var cliente = new ClienteHerramientasMcp((referencia, cancelacion) =>
{
    var a = new Pipe(); var b = new Pipe();
    IAsyncDisposable servidor = new ServidorAsistenteMcp(a.Reader.AsStream(), b.Writer.AsStream(), consultas, referencia, cancelacion);
    return Task.FromResult((b.Reader.AsStream(), a.Writer.AsStream(), servidor));
});
using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(60));
await using (var mcp = await cliente.ConectarAsync(instante, limite.Token))
{
    var catalogo = await mcp.DescubrirAsync(limite.Token);
    Comprobar(catalogo.Count == 6 && catalogo.Select(h => h.Nombre).Order().SequenceEqual(CatalogoAsistente.Nombres.Order()), "Negociación y descubrimiento MCP reales: seis herramientas de solo lectura");
    async Task<ResultadoOperativoDto> Invocar(string nombre, ConsultaOperativaDto consulta) => await mcp.InvocarAsync(
        new(Guid.NewGuid().ToString("N"), nombre, JsonSerializer.Serialize(new { consulta })), limite.Token);
    var busqueda = await Invocar("BuscarElementos", new() { Tipo = "Panel", Nombre = "Panel 3" });
    Console.WriteLine($"Diagnóstico depurado búsqueda: {busqueda.Codigo}; Total={busqueda.Total}; {busqueda.Mensaje}");
    Comprobar(busqueda.Codigo == CodigoAsistente.Exito && busqueda.Total == 4, "Homónimos conservan candidatos y pertenencias");
    var pagina = await Invocar("BuscarElementos", new() { Tipo = "Panel" });
    Comprobar(pagina.Total == 120 && pagina.PaginaSiguiente == 2 && Datos(pagina).GetArrayLength() == 50, "Búsqueda paginada sin restringir el total");
    var identificadores = new HashSet<Guid>();
    for (var numero = 1; numero <= 3; numero++)
    {
        var lote = await Invocar("BuscarElementos", new() { Tipo = "Panel", Pagina = numero });
        foreach (var elemento in Datos(lote).EnumerateArray())
            if (!identificadores.Add(elemento.GetProperty("Id").GetGuid())) throw new InvalidOperationException("Páginas duplicadas.");
    }
    Comprobar(identificadores.Count == 120, "Tres páginas sin omisiones");
    var estado = await Invocar("ConsultarEstadoActual", new());
    Comprobar(estado.Codigo == CodigoAsistente.Exito, "Estado actual por MCP y MySQL");
    var total = Datos(estado).GetProperty("Total");
    Comprobar(total.GetProperty("PanelesVigentes").GetInt32() == 118 && total.GetProperty("PanelesAntiguos").GetInt32() == 1
        && total.GetProperty("PanelesSinMedicion").GetInt32() == 1, "Estado distingue vigente, antiguo y ausente");
    Comprobar(total.GetProperty("PotenciaVigenteW").GetDecimal() == 11700, "Potencia vigente suma lecturas e incluye cero medido");
    var resumen = await Invocar("ResumirMediciones", new() { Agrupacion = "Global" });
    Comprobar(resumen.Codigo == CodigoAsistente.Exito && Datos(resumen)[0].GetProperty("Potencia").GetProperty("PanelesEsperados").GetInt32() == 120,
        "Resumen global con más de 100 paneles y todas las muestras");
    Comprobar(Datos(resumen)[0].GetProperty("Potencia").GetProperty("PromedioObservadoW").GetDecimal() == 11740,
        "Agregación global suma promedios por panel aunque tengan distinta cantidad de muestras");
    var cero = await Invocar("ResumirMediciones", new() { Tipo = "Panel", Id = paneles[0].ID, Agrupacion = "Global" });
    var ausente = await Invocar("ResumirMediciones", new() { Tipo = "Panel", Id = paneles[119].ID, Agrupacion = "Global" });
    Comprobar(cero.Codigo == CodigoAsistente.Exito && Datos(cero)[0].GetProperty("Potencia").GetProperty("PromedioObservadoW").GetDecimal() == 0
        && ausente.Codigo == CodigoAsistente.SinDatos, "Cero real y ausencia de mediciones son distintos");
    var historialConsulta = new ConsultaOperativaDto { Tipo = "Panel", Id = paneles[1].ID, TamanoPagina = 50 };
    var historial = await Invocar("ConsultarHistorial", historialConsulta);
    Comprobar(historial.Codigo == CodigoAsistente.Exito && historial.Cursor is not null && Datos(historial).GetProperty("Muestras").GetArrayLength() == 50, "Historial primera página y continuación explícita");
    var segunda = await Invocar("ConsultarHistorial", historialConsulta with { Cursor = historial.Cursor });
    Comprobar(segunda.Codigo == CodigoAsistente.Exito && segunda.Cursor is null && Datos(segunda).GetProperty("Muestras").GetArrayLength() == 39,
        "Cursor estable sin duplicados ni omisiones; intervalo semiabierto excluye muestra en hasta");
    var comparar = await Invocar("CompararMediciones", new() { Tipo = "Nodo", Id = nodos[0].ID, Agrupacion = "Nodo" });
    Comprobar(comparar.Codigo == CodigoAsistente.Exito && comparar.Periodo!.Descripcion.Contains("igual tramo"), "Comparación usa períodos equivalentes en Montevideo");
    Comprobar(Datos(comparar)[0].GetProperty("Comparacion").GetProperty("DiferenciaW").GetDecimal() == -3100
        && Datos(comparar)[0].GetProperty("Comparacion").GetProperty("Conclusion").GetString() == "NoConcluyente",
        "Diferencias conocidas y ausencia de ganador con cobertura incompleta");
    var tramo = new ConsultaOperativaDto { Tipo = "Panel", Id = paneles[1].ID, Agrupacion = "Global", Periodo = "Explicito",
        Desde = instante.AddDays(-40).ToString("O"), Hasta = instante.AddDays(-10).ToString("O") };
    await Invocar("ResumirMediciones", tramo);
    var compuesto = await Invocar("ResumirMediciones", tramo with { Desde = instante.AddDays(-10).ToString("O"), Hasta = instante.ToString("O"), ComponerConAnterior = true });
    Comprobar(compuesto.Codigo == CodigoAsistente.Exito && compuesto.Periodo!.HastaUtc - compuesto.Periodo.DesdeUtc == TimeSpan.FromDays(40)
        && Datos(compuesto)[0].GetProperty("Muestras").GetProperty("Cantidad").GetInt64() == 119,
        "Composición de 40 días en tramos: todos los acumulados y huecos calculados en SolarUY");
    var invalido = await Invocar("ResumirMediciones", new() { TamanoPagina = 201 });
    Comprobar(invalido.Codigo == CodigoAsistente.ArgumentosInvalidos, "Límite de página validado");
    var escritura = await Invocar("EliminarPanel", new());
    Comprobar(escritura.Codigo == CodigoAsistente.ArgumentosInvalidos, "Escritura fuera de catálogo rechazada");
    var suplantacion = await mcp.InvocarAsync(new("falsa", "BuscarElementos", "{\"consulta\":{\"Tipo\":\"Panel\",\"rol\":\"Administrador\"}}"), limite.Token);
    Comprobar(suplantacion.Codigo == CodigoAsistente.ArgumentosInvalidos, "Argumentos de identidad fuera de esquema rechazados");
    usuario.Autorizado = false;
    Comprobar((await Invocar("ConsultarEstadoActual", new())).Codigo == CodigoAsistente.AccesoDenegado, "Autorización revalidada en cada herramienta MCP");
    usuario.Autorizado = true;
}

// Inyecciones explícitas: verificaciones auxiliares del adaptador, no aceptación externa Gemini.
foreach (var caso in new[] { (HttpStatusCode.TooManyRequests, CodigoAsistente.CuotaAgotada), (HttpStatusCode.ServiceUnavailable, CodigoAsistente.FalloProveedor) })
{
    var modelo = new GeminiConversacional(new HttpClient(new RespuestaHttp(caso.Item1)), opciones, NullLogger<GeminiConversacional>.Instance);
    var respuesta = await modelo.CompletarAsync(new("Consulta funcional", [new("user", "Hola")], [], []), default);
    Comprobar(respuesta.Codigo == caso.Item2, "Fallo Gemini inyectado: " + caso.Item2);
    if (caso.Item2 == CodigoAsistente.CuotaAgotada) Comprobar(respuesta.ReintentarEnSegundos == 17, "Retry-After conservado");
}
Comprobar(periodos.Resolver("Hoy", null, null, instante).DesdeUtc.Hour == 3, "Medianoche Montevideo convertida a UTC");
Comprobar(periodos.Resolver("Explicito", "2026-11-05T00:00:00", "2026-11-05T12:00:00", instante).DesdeUtc.Hour == 3,
    "Fechas sin offset interpretadas en Montevideo, independientes de la zona del proceso");
await VerificacionConversacion.EjecutarAsync(cliente, usuario, periodos, opciones);
Console.WriteLine("Verificaciones auxiliares completadas. Gemini, OIDC y balanceador reales pendientes de aceptación desde el chat.");

static JsonElement Datos(ResultadoOperativoDto resultado) => JsonSerializer.SerializeToElement(resultado.Datos);
static void Comprobar(bool condicion, string descripcion)
{
    if (!condicion) throw new InvalidOperationException("FALLÓ: " + descripcion);
    Console.WriteLine("OK: " + descripcion);
}

sealed class ContextoFactory(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
}
sealed class RelojFijo(DateTimeOffset instante) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instante;
}
sealed class UsuarioFuncional(DateTimeOffset instante) : IUsuarioActualService
{
    public bool Autorizado { get; set; } = true;
    public bool EstaAutenticado => Autorizado;
    public bool PerteneceAlRol(string rol) => Autorizado && rol is "Administrador" or "Operador";
    public Task<SesionAsistenteDto?> ObtenerSesionAsistenteAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SesionAsistenteDto?>(Autorizado ? new("funcional-aislada", instante.AddMinutes(-1), instante.AddMinutes(30)) : null);
}
sealed class RespuestaHttp(HttpStatusCode codigo) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var respuesta = new HttpResponseMessage(codigo) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        respuesta.Headers.RetryAfter = new(TimeSpan.FromSeconds(17));
        return Task.FromResult(respuesta);
    }
}
