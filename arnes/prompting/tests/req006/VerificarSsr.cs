// Host auxiliar local con componentes, servicios y MySQL reales. No acredita OIDC externo.
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SolarUY.Application.Nodos;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.Paneles;
using SolarUY.Application.Paneles.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Components;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using static VerificarAltaPanel;

internal static class VerificarSsr
{
    internal const string Direccion = "http://127.0.0.1:8196";

    internal static async Task EjecutarAsync(bool preview)
    {
        var conexion = ConexionAislada();
        var rutas = JsonDocument.Parse(await File.ReadAllTextAsync(Carpeta + "/rutas.json")).RootElement;
        var nodoId = rutas.GetProperty("nodo").GetGuid();
        var segundoId = rutas.GetProperty("segundo").GetGuid();
        var parqueId = rutas.GetProperty("parque").GetGuid();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(App).Assembly.FullName,
            ContentRootPath = Path.Combine(Directory.GetCurrentDirectory(), "SolarUY"), EnvironmentName = "Production"
        });
        builder.WebHost.UseUrls(Direccion);
        builder.WebHost.UseSetting(WebHostDefaults.StaticWebAssetsKey, Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.runtime.json"));
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        var diagnosticos = new DiagnosticosProvider();
        builder.Logging.AddProvider(diagnosticos);
        var fallo = new FalloControladoInterceptor();
        builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddDbContext<SolarUyDbContext>(options =>
        {
            PersistenciaConfiguration.ConfigurarContexto(options, conexion);
            options.AddInterceptors(fallo);
        });
        builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
        builder.Services.AddScoped<INodoRepository, NodoRepository>();
        builder.Services.AddScoped<SolarUY.Application.Monitoreo.IMedicionRepository, MedicionRepositorySinLecturas>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddScoped<IPanelRepository, PanelRepository>();
        builder.Services.AddScoped<IPanelService, PanelService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "SolarUY.Req006.Auxiliar";
            options.LoginPath = "/cuenta/iniciar-sesion";
            options.AccessDeniedPath = "/cuenta/acceso-denegado";
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        await using var app = builder.Build();
        app.UseRequestLocalization(options => options.SetDefaultCulture("es-UY").AddSupportedCultures("es-UY").AddSupportedUICultures("es-UY"));
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery(); app.UseStaticFiles();
        app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "SolarUY.staticwebassets.endpoints.json"));
        app.MapGet("/_auxiliar/sesion/{rol}", async (HttpContext contexto, string rol) =>
        {
            if (rol is not ("Administrador" or "Operador")) return Results.BadRequest();
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Sesión auxiliar req006"), new Claim(ClaimTypes.Role, rol)],
                    CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Redirect("/parques-solares");
        });
        app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);
        await app.StartAsync();
        try
        {
            using var admin = Cliente(); using var operador = Cliente(); using var anonimo = Cliente();
            await admin.GetAsync("/_auxiliar/sesion/Administrador");
            await operador.GetAsync("/_auxiliar/sesion/Operador");
            var ruta = $"/nodos/{nodoId:D}/paneles/nuevo";
            var detalleUrl = $"/nodos/{nodoId:D}";
            await using var db = Contexto(conexion);
            var cantidadAntes = await db.Paneles.CountAsync();
            foreach (var cliente in new[] { anonimo, operador })
            {
                foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Post })
                {
                    using var peticion = new HttpRequestMessage(metodo, ruta);
                    if (metodo == HttpMethod.Post) peticion.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["Formulario.Nombre"] = "Manipulado" });
                    using var respuesta = await cliente.SendAsync(peticion);
                    Exigir(respuesta.StatusCode == HttpStatusCode.Redirect && respuesta.Headers.Location!.ToString()
                        .Contains(cliente == anonimo ? "/cuenta/iniciar-sesion" : "/cuenta/acceso-denegado"), "ssr-permisos-get-post-identidad-auxiliar");
                    Exigir(!(await respuesta.Content.ReadAsStringAsync()).Contains("Nodo de destino"), "ssr-rechazo-sin-datos");
                }
                using var rechazoDetalle = await cliente.GetAsync(detalleUrl);
                Exigir(rechazoDetalle.StatusCode == HttpStatusCode.Redirect, "ssr-detalle-protegido");
            }
            Exigir(await db.Paneles.CountAsync() == cantidadAntes, "ssr-rechazos-no-escriben");
            var formulario = await admin.GetStringAsync(ruta);
            var texto = WebUtility.HtmlDecode(formulario);
            Exigir(texto.Contains("Nodo de destino:") && texto.Contains("Nodo A"), "ssr-destino-correcto");
            Exigir(Regex.Matches(formulario, "<input[^>]+id=\"(?:nombre|modelo|potencia)-panel\"").Count == 3
                && texto.Contains("Nombre *") && texto.Contains("Modelo *") && texto.Contains("Potencia nominal (W) *"), "ssr-solo-tres-entradas");
            Exigir(formulario.Contains("href=\"" + detalleUrl + "\"") && texto.Contains(">Cancelar</a>"), "ssr-cancelar-al-mismo-nodo");
            foreach (var id in new[] { "nombre-panel", "modelo-panel", "potencia-panel" })
                Exigir(formulario.Contains("for=\"" + id + "\"") && formulario.Contains("aria-describedby=\"" + id + "-error\""), "ssr-etiquetas-mensajes-asociados");
            Exigir(!Regex.IsMatch(Regex.Replace(texto, "<[^>]+>", " "), "[a-f0-9]{8}-[a-f0-9-]{27}"), "ssr-sin-ids-visibles");
            using (var sinToken = await admin.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string, string> { ["_handler"] = "alta-panel" })))
                Exigir(sinToken.StatusCode == HttpStatusCode.BadRequest, "ssr-antifalsificacion");
            foreach (var caso in new[]
            {
                ("Nombre", "", "El nombre es obligatorio."), ("Nombre", new string('X', 101), "100 caracteres"),
                ("Modelo", "   ", "El modelo es obligatorio."), ("PotenciaNominalW", "", "La potencia nominal es obligatoria."),
                ("PotenciaNominalW", "0", "mayor que cero"), ("PotenciaNominalW", "-1", "mayor que cero"),
                ("PotenciaNominalW", "texto", "debe ser un número")
            })
            {
                var campos = Campos(formulario, "Validación"); campos["Formulario." + caso.Item1] = caso.Item2;
                using var respuesta = await admin.PostAsync(ruta, new FormUrlEncodedContent(campos));
                var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
                Exigir(respuesta.StatusCode == HttpStatusCode.OK && html.Contains(caso.Item3)
                    && html.Contains(caso.Item1 == "Modelo" ? "value=\"Validación\"" : "value=\"Modelo SSR\""), "ssr-validacion-conserva-entradas-" + caso.Item1);
                Exigir(html.Contains("validation-message"), "ssr-error-de-campo");
            }
            Exigir(await db.Paneles.CountAsync() == cantidadAntes, "ssr-validaciones-sin-escrituras");
            var nombre = "Panel SSR " + Guid.NewGuid().ToString("N")[..8];
            var alta = Campos(formulario, nombre);
            alta["Formulario.ID"] = Guid.NewGuid().ToString(); alta["Formulario.NodoId"] = segundoId.ToString();
            alta["Formulario.Estado"] = "Desactivado"; alta["Formulario.FechaInstalacion"] = "2000-01-01";
            var antes = DateTime.UtcNow.AddSeconds(-1);
            using (var respuesta = await admin.PostAsync(ruta, new FormUrlEncodedContent(alta)))
            {
                Exigir(respuesta.StatusCode == HttpStatusCode.Redirect && respuesta.Headers.Location!.ToString().EndsWith(detalleUrl + "?panelRegistrado=true", StringComparison.Ordinal), "ssr-post-redirect-get");
            }
            var persistido = await db.Paneles.AsNoTracking().Include(p => p.Nodo).SingleAsync(p => p.Nombre.Valor == nombre);
            Exigir(persistido.Nodo.ID == nodoId && persistido.ID.ToString() != alta["Formulario.ID"]
                && persistido.Estado.ToString() == "Operativo" && persistido.PotenciaNominalW == 600.25f
                && persistido.FechaInstalacion >= antes && persistido.FechaInstalacion <= DateTime.UtcNow
                && persistido.FechaInstalacion.Ticks % TimeSpan.TicksPerSecond == 0, "ssr-cultura-coma-y-post-manipulado-no-controla-alta");
            var detalle = WebUtility.HtmlDecode(await admin.GetStringAsync(detalleUrl + "?panelRegistrado=true"));
            Exigir(detalle.Contains("Panel solar registrado") && detalle.Contains(nombre) && detalle.Contains("600,25 W")
                && detalle.Contains(" UTC") && detalle.Contains("Operativo") && detalle.Contains("Sin mediciones") && detalle.Contains("Sin mantenimientos"), "ssr-lista-confirmacion-valores-derivados");
            Exigir(detalle.Contains("inversor-titulo") && detalle.Contains("sensor-titulo") && detalle.Contains("href=\"" + ruta + "\""), "ssr-conserva-equipos-enlace-alta");
            await admin.GetStringAsync(detalleUrl + "?panelRegistrado=true");
            Exigir(await db.Paneles.CountAsync() == cantidadAntes + 1, "ssr-recargar-no-reenvia-alta");
            using (var respuesta = await admin.PostAsync(ruta, new FormUrlEncodedContent(Campos(formulario, " " + nombre.ToUpperInvariant() + " "))))
                Exigir(WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()).Contains("Ya existe un panel solar con ese nombre en este nodo."), "ssr-duplicado-mensaje");
            var segundoUrl = $"/nodos/{segundoId:D}/paneles/nuevo";
            using (var respuesta = await admin.PostAsync(segundoUrl, new FormUrlEncodedContent(Campos(await admin.GetStringAsync(segundoUrl), nombre))))
                Exigir(respuesta.StatusCode == HttpStatusCode.Redirect, "ssr-mismo-nombre-otro-nodo");
            foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
            {
                var ausente = WebUtility.HtmlDecode(await admin.GetStringAsync($"/nodos/{id:D}/paneles/nuevo"));
                Exigir(ausente.Contains("No se encontró el nodo") && ausente.Contains("Volver al listado") && !ausente.Contains("id=\"nombre-panel\"")
                    && !ausente.Contains("Nodo de destino"), "ssr-nodo-ausente-sin-formulario");
            }
            using (var malformado = await admin.GetAsync("/nodos/no-es-guid/paneles/nuevo"))
                Exigir(malformado.StatusCode == HttpStatusCode.NotFound, "ssr-guid-malformado");
            fallo.Lectura = true;
            try
            {
                var fallido = WebUtility.HtmlDecode(await admin.GetStringAsync(ruta));
                Exigir(fallido.Contains("No se pudo cargar la información") && fallido.Contains("Reintentar") && !fallido.Contains("Nodo de destino"), "ssr-fallo-carga-sin-datos-reintento");
                var fallidoDetalle = WebUtility.HtmlDecode(await admin.GetStringAsync(detalleUrl));
                Exigir(fallidoDetalle.Contains("No se pudo cargar la información") && !fallidoDetalle.Contains("Este nodo todavía no tiene paneles"), "ssr-fallo-no-es-lista-vacia");
            }
            finally { fallo.Lectura = false; }
            fallo.Guardado = true;
            try
            {
                using var respuesta = await admin.PostAsync(ruta, new FormUrlEncodedContent(Campos(await admin.GetStringAsync(ruta), "Fallo SSR")));
                var fallido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
                Exigir(fallido.Contains("No se pudo registrar el panel solar") && fallido.Contains("value=\"Fallo SSR\"")
                    && !fallido.Contains("Panel solar registrado"), "ssr-fallo-guardado-conserva-entradas");
            }
            finally { fallo.Guardado = false; }
            using (var respuesta = await admin.PostAsync(ruta, new FormUrlEncodedContent(Campos(await admin.GetStringAsync(ruta), "Recuperación SSR " + nombre))))
                Exigir(respuesta.StatusCode == HttpStatusCode.Redirect, "ssr-recuperacion-tras-fallo");
            Exigir(diagnosticos.Mensajes.Contains("Código: carga_alta_panel.") && diagnosticos.Mensajes.Contains("Código: alta_panel.")
                && diagnosticos.Mensajes.All(m => !m.Contains("SELECT") && !m.Contains("INSERT") && !m.Contains("Exception")), "ssr-diagnosticos-depurados");
            using var datosScope = app.Services.CreateScope();
            var nodosDb = datosScope.ServiceProvider.GetRequiredService<SolarUyDbContext>();
            var nodoService = new NodoService(new NodoRepository(nodosDb), new ParqueSolarRepository(nodosDb), Usuario("Administrador"), new MedicionRepositorySinLecturas());
            var serie = Guid.NewGuid().ToString("N");
            var nuevoNodo = await nodoService.CrearAsync(parqueId, new CrearNodoDto
            { Nombre = "Nodo vacío req006", Latitud = 0, Longitud = 0, NombreInversor = "Inversor vacío", NumeroSerieInversor = "I-" + serie,
                NombreSensor = "Sensor vacío", NumeroSerieSensor = "S-" + serie });
            var vacio = WebUtility.HtmlDecode(await admin.GetStringAsync($"/nodos/{nuevoNodo.Nodo!.ID:D}"));
            Exigir(vacio.Contains("Este nodo todavía no tiene paneles solares"), "ssr-nodo-vacio-y-regresion-alta-nodos");
            var parque = WebUtility.HtmlDecode(await admin.GetStringAsync($"/parques-solares/{parqueId:D}"));
            var listado = WebUtility.HtmlDecode(await admin.GetStringAsync("/parques-solares"));
            Exigir(parque.Contains("Nodo vacío req006") && parque.Contains("Capacidad total estimada") && listado.Contains("Parque req006 A"), "ssr-regresion-parques");
            await File.WriteAllLinesAsync(Carpeta + "/ssr-resultados.txt", Resultados);
            await File.WriteAllTextAsync(Carpeta + "/preview.json", JsonSerializer.Serialize(new { Url = Direccion + detalleUrl, Vacio = Direccion + "/nodos/" + nuevoNodo.Nodo.ID }));
            if (preview)
            {
                await File.WriteAllTextAsync(Carpeta + "/preview.pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("PREVIEW " + Direccion + detalleUrl + " (solo identidades auxiliares)");
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
        }
        finally { await app.StopAsync(); }
    }

    private static HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        { BaseAddress = new Uri(Direccion) };

    private static Dictionary<string, string> Campos(string html, string nombre)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Exigir(token.Success, "ssr-token-presente");
        return new()
        {
            ["_handler"] = "alta-panel", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value),
            ["Formulario.Nombre"] = nombre, ["Formulario.Modelo"] = "Modelo SSR", ["Formulario.PotenciaNominalW"] = "600,25"
        };
    }

    private sealed class FalloControladoException : Exception;
    private sealed class FalloControladoInterceptor : DbCommandInterceptor, ISaveChangesInterceptor
    {
        internal bool Lectura { get; set; }
        internal bool Guardado { get; set; }
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command,
            CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
            => Lectura ? throw new FalloControladoException() : ValueTask.FromResult(result);
        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => Guardado ? throw new FalloControladoException() : ValueTask.FromResult(result);
    }

    private sealed class DiagnosticosProvider : ILoggerProvider
    {
        internal readonly List<string> Mensajes = [];
        public ILogger CreateLogger(string categoryName) => new DiagnosticosLogger(categoryName, Mensajes);
        public void Dispose() { }
    }
    private sealed class DiagnosticosLogger(string categoria, List<string> mensajes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => categoria.StartsWith("SolarUY.Presentation.", StringComparison.Ordinal) && logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(logLevel)) mensajes.Add(formatter(state, exception)); }
    }
}
