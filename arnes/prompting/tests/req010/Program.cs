using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SolarUY.Application.Energia;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Monitoreo;

try
{
    var conexion = new MySqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("SOLARUY_REQ010_TEST_MYSQL") ?? "");
    if (conexion.Server is not ("127.0.0.1" or "localhost") ||
        !conexion.Database.StartsWith("solaruy_req010_", StringComparison.Ordinal))
        throw new InvalidOperationException("Indicá un destino local y dedicado a req010.");

    var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
    PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
    await using var contexto = new SolarUyDbContext(opciones.Options);
    Exigir(!await contexto.Database.CanConnectAsync(), "El esquema de prueba debe ser nuevo.");
    await contexto.Database.MigrateAsync();

    var inicio = new DateTimeOffset(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);
    var parque = ParqueSolar.Crear("Parque de prueba", "Sur");
    var vecino = ParqueSolar.Crear("Parque vecino", "Norte");
    var nodoA = AgregarNodo(parque, "A");
    var nodoB = AgregarNodo(parque, "B");
    var vacio = AgregarNodo(parque, "Vacío");
    var nodoVecino = AgregarNodo(vecino, "Vecino");
    Panel AgregarPanel(Nodo nodo, string nombre) => nodo.AgregarPanel(nombre, "Modelo", 600, inicio.UtcDateTime);
    var panelA = AgregarPanel(nodoA, "A");
    var sinMedicion = AgregarPanel(nodoA, "Sin mediciones");
    var panelB = AgregarPanel(nodoB, "B");
    var enCero = AgregarPanel(nodoB, "Cero");
    var ajeno = AgregarPanel(nodoVecino, "Ajeno");

    contexto.AddRange(parque, vecino);
    void Medir(Panel panel, int segundos, float potencia) => contexto.Add(
        Medicion.Crear(panel, inicio.AddSeconds(segundos).UtcDateTime, potencia, EstadoPanel.Operativo));
    Medir(panelA, -20, 360);
    Medir(panelA, 20, 360);
    Medir(panelA, 61, 360);
    Medir(panelB, 0, 540);
    Medir(panelB, 20, 540);
    Medir(panelB, 62, 540);
    Medir(enCero, 0, 0);
    Medir(enCero, 20, 0);
    Medir(ajeno, 0, 600);
    Medir(ajeno, 20, 600);
    await contexto.SaveChangesAsync();

    var reloj = new RelojPrueba(inicio.AddSeconds(62));
    var usuario = new UsuarioPrueba();
    var servicio = new EnergiaService(new EnergiaRepository(new FabricaContexto(opciones.Options)), usuario, reloj);
    Task<EnergiaEstimadaDto?> Consultar(TipoComponenteEnergia tipo, Guid id) => servicio.ConsultarHoyAsync(tipo, id);
    var estimacionPanelA = await Consultar(TipoComponenteEnergia.Panel, panelA.ID);
    var estimacionNodoA = await Consultar(TipoComponenteEnergia.Nodo, nodoA.ID);
    var estimacionNodoB = await Consultar(TipoComponenteEnergia.Nodo, nodoB.ID);
    var estimacionParque = await Consultar(TipoComponenteEnergia.Parque, parque.ID);
    Exigir(estimacionPanelA is { EnergiaKwh: 0.002m, Cobertura: CoberturaEnergia.Parcial }, "Panel con hueco.");
    Exigir(estimacionNodoA is { EnergiaKwh: 0.002m, PanelesEsperados: 2, PanelesConDatos: 1 }, "Subtotal del nodo A.");
    Exigir(estimacionNodoB is { EnergiaKwh: 0.003m, PanelesEsperados: 2, PanelesConDatos: 2 }, "Subtotal del nodo B.");
    Exigir(estimacionParque is { EnergiaKwh: 0.005m, Cobertura: CoberturaEnergia.Parcial, PanelesEsperados: 4 }
        && estimacionParque.InicioLocal.Hour == 0 && estimacionParque.CorteLocal.Offset == TimeSpan.FromHours(-3),
        "Total del parque, exclusión del panel ajeno y corte en hora de Uruguay.");
    Exigir((await Consultar(TipoComponenteEnergia.Panel, enCero.ID))?.EnergiaKwh == 0m, "Cero medido.");
    Exigir((await Consultar(TipoComponenteEnergia.Panel, sinMedicion.ID))?.Cobertura ==
        CoberturaEnergia.SinDatosSuficientes, "Panel sin mediciones.");
    Exigir((await Consultar(TipoComponenteEnergia.Nodo, vacio.ID))?.Cobertura ==
        CoberturaEnergia.SinPaneles, "Nodo sin paneles.");

    usuario.Rol = "Visitante";
    Exigir(await Consultar(TipoComponenteEnergia.Parque, parque.ID) is null, "Acceso al indicador restringido.");
    usuario.Rol = "Administrador";
    reloj.Instante = reloj.Instante.AddSeconds(1);
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-UY");
    using var servicios = new ServiceCollection().AddLogging().AddSingleton(servicio).BuildServiceProvider();
    await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var parametros = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(IndicadorEnergia.Tipo)] = TipoComponenteEnergia.Parque,
            [nameof(IndicadorEnergia.ComponenteId)] = parque.ID
        });
        return WebUtility.HtmlDecode((await renderer.RenderComponentAsync<IndicadorEnergia>(parametros)).ToHtmlString());
    });
    Exigir(html.Contains("0,005 kWh") && html.Contains("Cobertura parcial") && html.Contains("00:01:03"),
        "Render del total de MySQL y nuevo corte local.");

    Console.WriteLine("MySQL: migraciones, mediciones, totales 0,002 / 0,003 / 0,005 kWh y render correctos.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Verificación fallida ({error.GetBaseException().GetType().Name}).");
    return 1;
}

static Nodo AgregarNodo(ParqueSolar parque, string nombre) =>
    parque.AgregarNodo(nombre, 0, 0, Inversor.Crear("Inversor", $"I-{nombre}", Sensor.Crear("Sensor", $"S-{nombre}")));

static void Exigir(bool condicion, string descripcion)
{
    if (!condicion)
    {
        Console.Error.WriteLine(descripcion);
        throw new InvalidOperationException();
    }
}

sealed class FabricaContexto(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
}

sealed class RelojPrueba(DateTimeOffset instante) : TimeProvider
{
    public DateTimeOffset Instante { get; set; } = instante;
    public override DateTimeOffset GetUtcNow() => Instante;
}

sealed class UsuarioPrueba : IUsuarioActualService
{
    public string Rol { get; set; } = "Operador";
    public bool EstaAutenticado => true;
    public bool PerteneceAlRol(string rol) => Rol == rol;
}
