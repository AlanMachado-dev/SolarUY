using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Asistente;

if (args.Contains("--seguimiento") || args.Contains("--seguimiento-gemini"))
{
    await VerificacionSeguimientoService.EjecutarAsync(args.Contains("--seguimiento-gemini"));
    return;
}

if (args.Contains("--preparar-oidc") || args.Contains("--actualizar-oidc"))
{
    try { await PreparacionAceptacionService.EjecutarAsync(args.Contains("--preparar-oidc")); }
    catch (Exception error)
    {
        Console.Error.WriteLine("Preparación OIDC aislada: " + error.GetType().Name + ". Sin detalles privados.");
        foreach (var marco in new System.Diagnostics.StackTrace(error, true).GetFrames().Take(8))
            Console.Error.WriteLine(marco.GetMethod()?.DeclaringType?.FullName + "." + marco.GetMethod()?.Name
                + ":" + marco.GetFileLineNumber());
        Environment.ExitCode = 1;
    }
    return;
}

// Destino fijo de esta herramienta. Nunca carga la conexión de appsettings.Local.json.
const string conexion = "server=127.0.0.1;port=43329;database=solaruy_mod009;user=root;password=;OldGuids=true";
var evidencia = Path.GetFullPath("artifacts/mod009");
Directory.CreateDirectory(evidencia);
var archivoEscenario = Path.Combine(evidencia, "escenario.json");
var resultados = new List<object>();
var reloj = new RelojFuncional(new(2026, 11, 18, 19, 3, 36, TimeSpan.Zero));
var opciones = new AsistenteOptions { Habilitado = true, ClaveApi = "auxiliar-sin-credencial" };
var formato = new FormatoRespuestaAsistenteService();
var usuario = new UsuarioFuncional(reloj);
var opcionesDb = new DbContextOptionsBuilder<SolarUyDbContext>();
PersistenciaConfiguration.ConfigurarContexto(opcionesDb, conexion);
var factory = new ContextoFactory(opcionesDb.Options);
var servicios = new ServiceCollection();
servicios.AddSingleton<TimeProvider>(reloj).AddSingleton(opciones).AddSingleton<IUsuarioActualService>(usuario)
    .AddSingleton<IDbContextFactory<SolarUyDbContext>>(factory).AddScoped<PeriodoConsultaService>()
    .AddScoped<FormatoRespuestaAsistenteService>().AddScoped<IConsultaOperativaRepository, ConsultaOperativaRepository>()
    .AddScoped<IConsultaOperativaService, ConsultaOperativaService>();
await using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
var cliente = new ClienteHerramientasMcp((instante, cancelacion) =>
{
    var ambito = proveedor.CreateAsyncScope();
    var entrada = new Pipe();
    var salida = new Pipe();
    IAsyncDisposable servidor = new ServidorConAmbito(new ServidorAsistenteMcp(entrada.Reader.AsStream(),
        salida.Writer.AsStream(), ambito.ServiceProvider.GetRequiredService<IConsultaOperativaService>(), instante, cancelacion), ambito);
    return Task.FromResult((salida.Reader.AsStream(), entrada.Writer.AsStream(), servidor));
});

try
{
    if (args.Contains("--preparar")) await PrepararAsync();
    var escenario = JsonSerializer.Deserialize<Escenario>(await File.ReadAllTextAsync(archivoEscenario))!;
    if (args.Contains("--web")) { await ServidorWebFuncional.EjecutarAsync(conexion, escenario); return; }
    await VerificarMcpAsync(escenario);
    await VerificarOrquestacionAsync(escenario);
    await VerificarProveedorAsync();
    await VerificarDependenciasAsync(escenario);
    if (args.Contains("--gemini")) await VerificarGeminiAsync(escenario);
}
catch (Exception error)
{
    // Los mensajes de excepciones de dependencias pueden contener información privada.
    resultados.Add(new { Caso = "interrupcion", Correcto = false, Error = error.GetType().Name });
    Console.Error.WriteLine("Comprobación interrumpida: " + error.GetType().Name);
    Environment.ExitCode = 1;
}
finally
{
    if (!args.Contains("--web")) await File.WriteAllTextAsync(Path.Combine(evidencia, "funcional.json"), JsonSerializer.Serialize(new
    {
        Tipo = "auxiliar_MCP_real_MySQL_aislado_sesion_sintetica",
        AceptacionExternaReal = false, Resultados = resultados
    }, new JsonSerializerOptions { WriteIndented = true }));
}

void Comprobar(bool condicion, string caso)
{
    resultados.Add(new { Caso = caso, Correcto = condicion });
    Console.WriteLine((condicion ? "OK " : "ERROR ") + caso);
    if (!condicion) throw new InvalidOperationException();
}

async Task PrepararAsync()
{
    await using var db = factory.CreateDbContext();
    await db.Database.EnsureCreatedAsync();
    Comprobar(!await db.ParquesSolares.AnyAsync(), "F01_destino_aislado_vacio");
    var parque = ParqueSolar.Crear("Parque mod009", "Escenario simulado aislado");
    Nodo Nodo(string nombre) => parque.AgregarNodo(nombre, 0, 0,
        Inversor.Crear("Inversor", Guid.NewGuid().ToString(), Sensor.Crear("Sensor", Guid.NewGuid().ToString())));
    var nodo = Nodo("Nodo principal");
    var otro = Nodo("Otro nodo");
    Panel Panel(string nombre, Nodo? destino = null) => (destino ?? nodo).AgregarPanel(nombre, "Simulado", 500, reloj.Instante.UtcDateTime.AddDays(-90));
    var principal = Panel("Panel principal");
    var parcial = Panel("Panel parcial");
    var vacio = Panel("Panel sin datos");
    var cero = Panel("Panel cero");
    var pequeno = Panel("Panel pequeño");
    var antiguo = Panel("Panel antiguo");
    Panel("Panel compartido"); Panel("Panel compartido", otro);
    Panel("Panel __Norte__ *A* <b>");
    db.Add(parque);
    void Lectura(Panel panel, DateTimeOffset fecha, float valor, EstadoPanel estado = EstadoPanel.Operativo) =>
        db.Add(Medicion.Crear(panel, fecha.UtcDateTime, valor, estado));
    for (var i = 0; i < 6; i++)
    {
        var fecha = reloj.Instante.AddSeconds(-120 + i * 20);
        Lectura(principal, fecha, new[] { 100.125f, 200.25f, 300.375f }[i % 3]);
        Lectura(cero, fecha, 0); Lectura(pequeno, fecha, 0.004f);
    }
    Lectura(principal, reloj.Instante.AddSeconds(-121), 999);
    Lectura(principal, reloj.Instante, 999); // Fin exclusivo: no debe participar del promedio.
    Lectura(parcial, reloj.Instante.AddSeconds(-70), 168.36876f);
    Lectura(antiguo, reloj.Instante.AddMinutes(-5), 40, EstadoPanel.EnFalla);
    var medianoche = new DateTimeOffset(2026, 11, 19, 3, 1, 0, TimeSpan.Zero);
    Lectura(principal, medianoche.AddSeconds(-120), 20);
    Lectura(principal, medianoche.AddSeconds(-60), 30);
    Lectura(principal, medianoche.AddSeconds(-20), 40);
    await db.SaveChangesAsync();
    await File.WriteAllTextAsync(archivoEscenario, JsonSerializer.Serialize(new Escenario(principal.ID, parcial.ID,
        vacio.ID, cero.ID, pequeno.ID, antiguo.ID, reloj.Instante)));
}

async Task<ResultadoOperativoDto> InvocarAsync(IConexionHerramientasMcp mcp, string nombre, ConsultaOperativaDto consulta) =>
    await mcp.InvocarAsync(new(Guid.NewGuid().ToString("N"), nombre, JsonSerializer.Serialize(new { consulta })), default);

ConsultaOperativaDto Reciente(Guid panel) => new() { Tipo = "Panel", Id = panel,
    Periodo = "Ultimos", CantidadPeriodo = 2, UnidadPeriodo = "Minutos", Agrupacion = "Panel" };

async Task VerificarMcpAsync(Escenario escenario)
{
    reloj.Instante = escenario.Instante;
    await using (var mcp = await cliente.ConectarAsync(reloj.Instante, default))
    {
        var catalogo = await mcp.DescubrirAsync(default);
        Comprobar(catalogo.Count == 6 && catalogo.Single(h => h.Nombre == "ResumirMediciones").EsquemaJson.Contains("CantidadPeriodo"), "F02_catalogo_real_con_periodo_relativo");
        var resumen = await InvocarAsync(mcp, "ResumirMediciones", Reciente(escenario.Principal));
        var datos = JsonSerializer.SerializeToElement(resumen.Datos)[0];
        Comprobar(resumen.Codigo == CodigoAsistente.Exito && resumen.Periodo!.DesdeUtc == escenario.Instante.AddMinutes(-2)
            && resumen.Periodo.HastaUtc == escenario.Instante, "F03_intervalo_exacto_del_turno");
        Comprobar(datos.GetProperty("Muestras").GetProperty("Cantidad").GetInt64() == 6
            && datos.GetProperty("Muestras").GetProperty("PromedioW").GetDecimal() == 200.25m, "F03_inicio_inclusivo_fin_exclusivo_y_promedio_MySQL");
        Comprobar(resumen.Presentacion!.Valores.Any(v => v.Campo == "Datos[0].Potencia.PromedioObservadoW" && v.Texto == "200,25 W")
            && resumen.Presentacion.Periodo!.Contains("16:01:36") && resumen.Presentacion.Periodo.Contains("16:03:36"), "F03_presentacion_y_consulta_coinciden");
        var historial = await InvocarAsync(mcp, "ConsultarHistorial", Reciente(escenario.Principal));
        Comprobar(JsonSerializer.SerializeToElement(historial.Datos).GetProperty("Muestras").GetArrayLength() == 6, "F03_historial_y_resumen_mismo_intervalo");
        var malas = new[]
        {
            Reciente(escenario.Principal) with { CantidadPeriodo = null },
            Reciente(escenario.Principal) with { CantidadPeriodo = 0 },
            Reciente(escenario.Principal) with { CantidadPeriodo = -1 },
            Reciente(escenario.Principal) with { CantidadPeriodo = double.MaxValue },
            Reciente(escenario.Principal) with { UnidadPeriodo = "Meses" },
            Reciente(escenario.Principal) with { Desde = "2026-11-18" },
            Reciente(escenario.Principal) with { DiasCompletos = true },
            new ConsultaOperativaDto { Tipo = "Panel", Id = escenario.Principal, Periodo = "Explicito", Desde = "2026-11-18T17:00:00-03:00", Hasta = "2026-11-18T16:00:00-03:00" }
        };
        foreach (var (mala, indice) in malas.Select((mala, indice) => (mala, indice)))
            Comprobar((await InvocarAsync(mcp, "ResumirMediciones", mala)).Codigo == CodigoAsistente.ArgumentosInvalidos, "F04_argumentos_invalidos_" + indice);
        var noFinita = await mcp.InvocarAsync(new("no-finita", "ResumirMediciones",
            "{\"consulta\":{\"Periodo\":\"Ultimos\",\"CantidadPeriodo\":1e999,\"UnidadPeriodo\":\"Minutos\"}}"), default);
        Comprobar(noFinita.Codigo == CodigoAsistente.ArgumentosInvalidos, "F04_duracion_no_finita_por_MCP");
        foreach (var (unidad, cantidad) in new[] { ("Segundos", 120d), ("Horas", 2d / 60), ("Dias", 2d / 1440) })
        {
            var equivalente = await InvocarAsync(mcp, "ResumirMediciones", Reciente(escenario.Principal) with { UnidadPeriodo = unidad, CantidadPeriodo = cantidad });
            Comprobar(Math.Abs((equivalente.Periodo!.HastaUtc - equivalente.Periodo.DesdeUtc).TotalSeconds - 120) < 0.00001, "F03_unidad_" + unidad);
        }
        foreach (var modo in new[] { "Hoy", "Ayer", "Manana", "Explicito" })
        {
            var calendario = new ConsultaOperativaDto { Tipo = "Panel", Id = escenario.Principal, Periodo = modo,
                Desde = modo == "Explicito" ? "2026-11-18T16:00:00-03:00" : null,
                Hasta = modo == "Explicito" ? "2026-11-18T16:03:36-03:00" : null };
            Comprobar((await InvocarAsync(mcp, "ResumirMediciones", calendario)).Codigo is CodigoAsistente.Exito or CodigoAsistente.SinDatos, "F05_calendario_" + modo);
        }
        Comprobar((await InvocarAsync(mcp, "CompararMediciones", new() { Tipo = "Panel", Id = escenario.Principal })).Referencia is not null, "F05_comparacion_conserva_referencia");
        var primero = await InvocarAsync(mcp, "ResumirMediciones", new() { Tipo = "Panel", Id = escenario.Principal,
            Periodo = "Explicito", Desde = "2026-11-18T16:00:00-03:00", Hasta = "2026-11-18T16:02:00-03:00" });
        var compuesto = await InvocarAsync(mcp, "ResumirMediciones", new() { Tipo = "Panel", Id = escenario.Principal,
            Periodo = "Explicito", Desde = "2026-11-18T16:02:00-03:00", Hasta = "2026-11-18T16:03:36-03:00", ComponerConAnterior = true });
        Comprobar(compuesto.Periodo?.DesdeUtc == primero.Periodo?.DesdeUtc && compuesto.Codigo == CodigoAsistente.Exito, "F05_composicion_conservada");
        foreach (var (panel, caso, codigo, texto) in new[]
        {
            (escenario.Vacio, "sin_datos", CodigoAsistente.SinDatos, "No hay mediciones disponibles"),
            (escenario.Cero, "cero_real", CodigoAsistente.Exito, "0 W"),
            (escenario.Pequeno, "positivo_pequeno", CodigoAsistente.Exito, "menos de 0,01 W"),
            (escenario.Parcial, "parcial", CodigoAsistente.Exito, "168,37 W")
        })
        {
            var resultado = await InvocarAsync(mcp, "ResumirMediciones", Reciente(panel));
            Comprobar(resultado.Codigo == codigo && resultado.Presentacion!.Valores.Any(v => v.Texto == texto), "F09_" + caso);
        }
        var estado = await InvocarAsync(mcp, "ConsultarEstadoActual", new() { Tipo = "Panel", Id = escenario.Antiguo });
        Comprobar(estado.Presentacion!.Valores.Any(v => v.Texto == "sin mediciones recientes") &&
            estado.Presentacion.Valores.Any(v => v.Texto == "en falla"), "F09_antiguedad_y_estado_legibles");
        var homonimos = await InvocarAsync(mcp, "BuscarElementos", new() { Tipo = "Panel", Nombre = "Panel compartido" });
        Comprobar(homonimos.Total == 2, "F08_candidatos_homonimos_conservados");
        var incompatible = await mcp.InvocarAsync(new("mala", "EscribirDatos", "{}"), default);
        Comprobar(incompatible.Codigo == CodigoAsistente.ArgumentosInvalidos && incompatible.InstanteUtc == escenario.Instante, "F12_solo_lectura_y_referencia_en_errores");
    }
    reloj.Instante = new(2026, 11, 19, 3, 1, 0, TimeSpan.Zero);
    await using (var mcp = await cliente.ConectarAsync(reloj.Instante, default))
    {
        await mcp.DescubrirAsync(default);
        var resultado = await InvocarAsync(mcp, "ResumirMediciones", Reciente(escenario.Principal));
        Comprobar(resultado.Presentacion!.Periodo!.Contains("18/11/2026 23:59:00") && resultado.Presentacion.Periodo.Contains("19/11/2026 00:01:00"), "F03_cruce_de_medianoche");
        var manana = await InvocarAsync(mcp, "ResumirMediciones", new() { Tipo = "Panel", Id = escenario.Principal, Periodo = "Manana" });
        Comprobar(manana.Codigo == CodigoAsistente.ArgumentosInvalidos && manana.Mensaje!.Contains("todavía no comenzó"), "F05_manana_no_iniciada");
    }
    reloj.Instante = escenario.Instante;
    var culturaAnterior = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Comprobar(formato.Potencia(168.36876m) == "168,37 W" && formato.Potencia(1.005m) == "1,01 W"
            && formato.Fecha(reloj.Instante).Contains("16:03:36"), "F09_formato_independiente_de_cultura_host");
    }
    finally { CultureInfo.CurrentCulture = culturaAnterior; }
}

async Task VerificarOrquestacionAsync(Escenario escenario)
{
    var periodos = new PeriodoConsultaService(reloj, opciones);
    var llamada = new LlamadaModeloDto("resumen", "ResumirMediciones", JsonSerializer.Serialize(new { consulta = Reciente(escenario.Principal) }));
    var modelo = new ModeloEscenario(turno => turno.Intercambios.Count == 0
        ? new(CodigoAsistente.Exito, null, [llamada])
        : new(CodigoAsistente.Exito, "**Promedio disponible:** 200,25 W.\n\\- Mediciones simuladas.", []));
    var asistente = new AsistenteService(usuario, modelo, cliente, periodos, opciones, formato);
    var pregunta = new PreguntaAsistenteDto("Promedio de los últimos dos minutos", "mod009-sintetica", [], null);
    var respuesta = await asistente.PreguntarAsync(pregunta, default);
    Comprobar(respuesta.Codigo == CodigoAsistente.Exito && respuesta.Mensaje.StartsWith("Promedio disponible:")
        && respuesta.Mensaje.Contains("• Mediciones") && !respuesta.Mensaje.Contains("**"), "F10_normalizacion_en_orquestacion_MCP_real_modelo_guionado");
    const string literal = "Panel __Norte__ *A* <b>";
    Comprobar(formato.NormalizarTexto("**Promedio:** " + literal + " 200,25 W", [literal]) == "Promedio: " + literal + " 200,25 W", "F10_preserva_nombre_literal_y_numeros");
    var invalido = new ModeloEscenario(_ => new(CodigoAsistente.Exito, null,
        [llamada with { ArgumentosJson = JsonSerializer.Serialize(new { consulta = Reciente(escenario.Principal) with { CantidadPeriodo = -1 } }) }]));
    var rechazado = await new AsistenteService(usuario, invalido, cliente, periodos, opciones, formato).PreguntarAsync(pregunta, default);
    Comprobar(rechazado.Codigo == CodigoAsistente.ArgumentosInvalidos && invalido.Llamadas == 1, "F04_no_redacta_exito_tras_periodo_invalido");
    foreach (var rol in new[] { "Administrador", "Operador", "Otro" })
    {
        usuario.Rol = rol;
        var resultado = await asistente.PreguntarAsync(pregunta, default);
        Comprobar(resultado.Codigo == (rol == "Otro" ? CodigoAsistente.AccesoDenegado : CodigoAsistente.Exito), "F12_rol_sintetico_" + rol);
    }
    usuario.Rol = "Administrador"; usuario.Vencida = true;
    Comprobar((await asistente.PreguntarAsync(pregunta, default)).Codigo == CodigoAsistente.AccesoDenegado, "F12_sesion_sintetica_vencida");
    usuario.Vencida = false;
}

async Task VerificarProveedorAsync()
{
    foreach (var (estado, codigo) in new[] { (HttpStatusCode.TooManyRequests, CodigoAsistente.CuotaAgotada), (HttpStatusCode.ServiceUnavailable, CodigoAsistente.FalloProveedor) })
    {
        using var http = new HttpClient(new ProveedorAislado(estado));
        var gemini = new GeminiConversacional(http, opciones, NullLogger<GeminiConversacional>.Instance);
        var resultado = await gemini.CompletarAsync(new("Auxiliar", [new("user", "Consulta")], [], []), default);
        Comprobar(resultado.Codigo == codigo, "F11_proveedor_aislado_" + (int)estado);
    }
}

async Task VerificarDependenciasAsync(Escenario escenario)
{
    var opcionesFallo = new DbContextOptionsBuilder<SolarUyDbContext>();
    PersistenciaConfiguration.ConfigurarContexto(opcionesFallo,
        conexion.Replace("database=solaruy_mod009;", "database=solaruy_mod009_inexistente;", StringComparison.Ordinal));
    var periodos = new PeriodoConsultaService(reloj, opciones);
    var consultas = new ConsultaOperativaService(new ConsultaOperativaRepository(new ContextoFactory(opcionesFallo.Options), opciones),
        usuario, periodos, opciones, formato);
    var clienteFallo = new ClienteHerramientasMcp((instante, cancelacion) =>
    {
        var entrada = new Pipe(); var salida = new Pipe();
        IAsyncDisposable servidor = new ServidorAsistenteMcp(entrada.Reader.AsStream(), salida.Writer.AsStream(), consultas, instante, cancelacion);
        return Task.FromResult((salida.Reader.AsStream(), entrada.Writer.AsStream(), servidor));
    });
    var modelo = new ModeloEscenario(_ => new(CodigoAsistente.Exito, null,
        [new("resumen", "ResumirMediciones", JsonSerializer.Serialize(new { consulta = Reciente(escenario.Principal) }))]));
    var pregunta = new PreguntaAsistenteDto("Consulta de fallo aislado", "mod009-sintetica", [], null);
    var resultado = await new AsistenteService(usuario, modelo, clienteFallo, periodos, opciones, formato).PreguntarAsync(pregunta, default);
    Comprobar(resultado.Codigo == CodigoAsistente.FalloPersistencia && modelo.Llamadas == 1,
        "F11_fallo_MySQL_real_por_MCP_no_equivale_a_sin_datos");
    var clienteInterrumpido = new ClienteHerramientasMcp((_, _) => throw new IOException());
    var falloMcp = await new AsistenteService(usuario, modelo, clienteInterrumpido, periodos, opciones, formato).PreguntarAsync(pregunta, default);
    Comprobar(falloMcp.Codigo == CodigoAsistente.FalloMcp, "F11_transporte_MCP_interrumpido_no_equivale_a_sin_datos");
}

async Task VerificarGeminiAsync(Escenario escenario)
{
    var privadas = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(".solaruy-private/asistente.private.json"), optional: false).Build();
    var opcionesGemini = new AsistenteOptions();
    privadas.GetSection("Asistente").Bind(opcionesGemini);
    if (!opcionesGemini.Disponible) { resultados.Add(new { Caso = "Gemini_real", Pendiente = "Configuración privada no disponible" }); return; }
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    var gemini = new GeminiConversacional(http, opcionesGemini, NullLogger<GeminiConversacional>.Instance);
    var asistente = new AsistenteService(usuario, gemini, cliente, new PeriodoConsultaService(reloj, opcionesGemini), opcionesGemini, formato);
    var pregunta = new PreguntaAsistenteDto("¿Cuál es el promedio de potencia de este panel en los últimos dos minutos?", "mod009-sintetica", [], new("Panel", escenario.Principal));
    var respuesta = await asistente.PreguntarAsync(pregunta, default);
    // No escribir el contenido de la respuesta ni la configuración privada.
    var primeraOracion = System.Text.RegularExpressions.Regex.Match(respuesta.Mensaje,
        @"\A.*?(?:[.!?](?=\s|$)|\r?\n|$)", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
    var comprobacion = new { Caso = "Gemini_real_sesion_sintetica", Codigo = respuesta.Codigo.ToString(),
        PromedioCorrecto = respuesta.Mensaje.Contains("200,25 W"),
        ResultadoAlComienzo = primeraOracion.Contains("200,25 W"),
        ElementoIdentificado = respuesta.Mensaje.Contains("Panel principal", StringComparison.OrdinalIgnoreCase),
        PeriodoCorrecto = respuesta.Mensaje.Contains("16:01:36") && respuesta.Mensaje.Contains("16:03:36"),
        SinISO = !System.Text.RegularExpressions.Regex.IsMatch(respuesta.Mensaje, @"\d{4}-\d{2}-\d{2}T"),
        SinMarkdown = !respuesta.Mensaje.Contains("**") && !respuesta.Mensaje.Contains("\\-"),
        SimulacionIndicada = System.Text.RegularExpressions.Regex.IsMatch(respuesta.Mensaje, @"simulad|simulaci", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
        CantidadCorrecta = System.Text.RegularExpressions.Regex.IsMatch(respuesta.Mensaje, @"\b(6|seis)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
        AceptacionChatReal = false };
    resultados.Add(comprobacion);
    Console.WriteLine(JsonSerializer.Serialize(comprobacion));
    Console.WriteLine("Gemini real, sesión sintética: " + respuesta.Codigo);
}

sealed record Escenario(Guid Principal, Guid Parcial, Guid Vacio, Guid Cero, Guid Pequeno, Guid Antiguo, DateTimeOffset Instante);
sealed class RelojFuncional(DateTimeOffset instante) : TimeProvider
{
    public DateTimeOffset Instante { get; set; } = instante;
    public override DateTimeOffset GetUtcNow() => Instante;
}
sealed class UsuarioFuncional(RelojFuncional reloj) : IUsuarioActualService
{
    public string Rol { get; set; } = "Administrador";
    public bool Vencida { get; set; }
    public bool EstaAutenticado => true;
    public bool PerteneceAlRol(string rol) => rol == Rol;
    public Task<SesionAsistenteDto?> ObtenerSesionAsistenteAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SesionAsistenteDto?>(new("mod009-sintetica", reloj.Instante.AddHours(-1), reloj.Instante.AddHours(Vencida ? -1 : 1)));
}
sealed class ContextoFactory(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
    public Task<SolarUyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}
sealed class ServidorConAmbito(ServidorAsistenteMcp servidor, AsyncServiceScope ambito) : IAsyncDisposable
{
    public async ValueTask DisposeAsync() { await servidor.DisposeAsync(); await ambito.DisposeAsync(); }
}
sealed class ModeloEscenario(Func<TurnoModeloDto, PasoModeloDto> responder) : IModeloConversacional
{
    public int Llamadas { get; private set; }
    public Task<PasoModeloDto> CompletarAsync(TurnoModeloDto turno, CancellationToken cancellationToken)
    { Llamadas++; return Task.FromResult(responder(turno)); }
}
sealed class ProveedorAislado(HttpStatusCode estado) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(estado));
}
