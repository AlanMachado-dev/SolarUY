using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Presentation.Monitoreo;

var instante = new DateTime(2026, 11, 16, 2, 5, 0, DateTimeKind.Utc);
var parque = ParqueSolar.Crear("Parque de verificación", "Montevideo");
var nodo = parque.AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")));
var panel = nodo.AgregarPanel("Panel", "Modelo", 600, instante);
var notificacion = new NotificacionMedicion();
var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Operador")], "Verificación local"));
var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddAuthorizationCore();
servicios.AddSingleton<AuthenticationStateProvider>(new AutenticacionVisual(usuario));
servicios.AddSingleton<IJSRuntime, JsVisual>();
servicios.AddSingleton(notificacion);
var repositorio = new MedicionesVisuales(panel);
servicios.AddSingleton(new MonitoreoService(repositorio, notificacion));
servicios.AddSingleton(new ControlPanelService(repositorio, notificacion));

await using var proveedor = servicios.BuildServiceProvider();
await using var renderizador = new HtmlRenderer(proveedor, proveedor.GetRequiredService<ILoggerFactory>());
await renderizador.Dispatcher.InvokeAsync(async () =>
{
    var vista = await renderizador.RenderComponentAsync<CascadingAuthenticationState>(ParameterView.FromDictionary(
        new Dictionary<string, object?>
        {
            ["ChildContent"] = (RenderFragment)(contenido =>
            {
                contenido.OpenComponent<LecturaEnVivo>(0);
                contenido.AddComponentParameter(1, "PanelId", panel.ID);
                contenido.CloseComponent();
            })
        }));

    var html = WebUtility.HtmlDecode(vista.ToHtmlString());
    Verificar(html.Contains("No disponible") && html.Contains("Aún no hay mediciones") && !html.Contains("rz-gauge"),
        "Lectura ausente sin valor ni medidor");

    var sinGeneracion = Medicion.Crear(panel, instante, 0, EstadoPanel.EnFalla);
    await notificacion.PublicarAsync(LecturaPanelDto.Desde(panel, sinGeneracion, 0));
    html = WebUtility.HtmlDecode(vista.ToHtmlString());
    Verificar(TextoVisible(html).Contains("0 W") && html.Contains("15/11/2026 23:05:00") && html.Contains("rz-gauge"),
        "Lectura de 0 W con medidor y cambio de fecha local");

    var conGeneracion = Medicion.Crear(panel, instante.AddSeconds(20), 532.126f, EstadoPanel.Operativo);
    await notificacion.PublicarAsync(LecturaPanelDto.Desde(panel, conGeneracion, 0));
    html = WebUtility.HtmlDecode(vista.ToHtmlString());
    Verificar(TextoVisible(html).Contains("532,13 W") && !html.Contains("UTC"),
        "Lectura posterior actualizada con coma decimal");
    var mantenimiento = new MantenimientoPanelDto(Guid.NewGuid(), "Limpieza", instante, "Técnico", EstadoMantenimiento.Pendiente);
    var programada = LecturaPanelDto.Desde(panel, conGeneracion, 1) with { Mantenimiento = mantenimiento };
    await notificacion.PublicarAsync(programada);
    html = WebUtility.HtmlDecode(vista.ToHtmlString());
    Verificar(html.Contains("Mantenimiento programado") && html.Contains("15/11/2026 23:05:00 (hora de Uruguay)"),
        "Mantenimiento programado con hora local");

    var enMantenimiento = programada with
    {
        EstadoControl = EstadoPanel.EnMantenimiento,
        RevisionControl = 2,
        Mantenimiento = mantenimiento with { Estado = EstadoMantenimiento.EnCurso }
    };
    await notificacion.PublicarAsync(enMantenimiento);
    html = WebUtility.HtmlDecode(vista.ToHtmlString());
    Verificar(TextoVisible(html).Contains("0 W") && !html.Contains("532,13") && html.Contains("Finalizar Mantenimiento"),
        "Operador ve 0 W durante mantenimiento y puede finalizarlo");
    Verificar(!html.Contains("Desactivar Panel") && !html.Contains("Simular Falla"), "Operador sin controles de activación");
});

static string TextoVisible(string html) => Regex.Replace(html, "<[^>]+>", "");

static void Verificar(bool condicion, string mensaje)
{
    if (!condicion)
        throw new InvalidOperationException(mensaje);
    Console.WriteLine($"OK {mensaje}");
}

sealed class AutenticacionVisual(ClaimsPrincipal usuario) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(usuario));
}

sealed class JsVisual : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        ValueTask.FromResult(default(TValue)!);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}

sealed class MedicionesVisuales(Panel panel) : IMedicionRepository, IControlPanelRepository
{
    public Task<LecturaPanelDto?> ConsultarAsync(Guid panelId, CancellationToken cancellationToken) =>
        Task.FromResult<LecturaPanelDto?>(panelId == panel.ID ? LecturaPanelDto.Desde(panel, null, 0) : null);

    public Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ResultadoAdmisionMedicion> AdmitirAsync(MuestraSensorDto muestra, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ResultadoControlPanel> EjecutarAsync(Guid panelId, OrdenControlPanel orden, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Guid>> BuscarVencidosAsync(DateTime ahoraUtc, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ResultadoControlPanel> IniciarVencidoAsync(Guid panelId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
