// Comprobación funcional con MySQL real aislado. No acredita un ingreso OIDC real.
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using SolarUY.Presentation.Pages;

internal static class VerificarConsultas
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "preview" }))
            {
                await ServirPreviewAsync();
                return 0;
            }
            await EjecutarAsync();
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { correcto = false,
                tipo = error.GetBaseException().GetType().Name,
                codigo = error.GetBaseException() is MySqlException mysql ? mysql.Number : 0 }));
            return 1;
        }
    }

    private static async Task EjecutarAsync()
    {
        var raiz = Directory.GetCurrentDirectory();
        var salida = Path.Combine(raiz, "artifacts", "req003");
        Directory.CreateDirectory(salida);
        var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(raiz, "SolarUY"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var opciones = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        var baseHabitual = opciones.Database;
        var baseAislada = "solaruy_req003_aceptacion_" + Guid.NewGuid().ToString("N")[..12];
        Exigir(baseHabitual != baseAislada, "destino-distinto-de-base-habitual");
        opciones.Database = "";
        await using var conexion = new MySqlConnection(opciones.ConnectionString);
        await conexion.OpenAsync();
        await using (var crear = conexion.CreateCommand())
        {
            crear.CommandText = $"CREATE DATABASE `{baseAislada}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin";
            await crear.ExecuteNonQueryAsync();
        }
        // Solo el nombre del destino; nunca se persiste la conexión ni las credenciales.
        await File.WriteAllTextAsync(Path.Combine(salida, "destino-aislado.json"),
            JsonSerializer.Serialize(new { Origen = "req003-consultas-aisladas", Base = baseAislada }));
        opciones.Database = baseAislada;
        var servicios = new ServiceCollection();
        servicios.AddLogging(builder => builder.ClearProviders());
        servicios.AddDbContext<SolarUyDbContext>(builder =>
            PersistenciaConfiguration.ConfigurarContexto(builder, opciones.ConnectionString));
        servicios.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        servicios.AddScoped<IUsuarioActualService, UsuarioActualService>();
        servicios.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
        servicios.AddScoped<IParqueSolarService, ParqueSolarService>();
        servicios.AddSingleton<NavigationManager, NavegacionLocal>();
        await using var proveedor = servicios.BuildServiceProvider();
        await using var scope = proveedor.CreateAsyncScope();
        var contexto = scope.ServiceProvider.GetRequiredService<SolarUyDbContext>();
        await contexto.Database.MigrateAsync();
        var usuario = proveedor.GetRequiredService<IHttpContextAccessor>();
        usuario.HttpContext = new DefaultHttpContext();
        var servicio = scope.ServiceProvider.GetRequiredService<IParqueSolarService>();
        var repositorio = scope.ServiceProvider.GetRequiredService<IParqueSolarRepository>();

        // Identidades locales para integrar el servicio; no son sesiones externas reales.
        Identificar(usuario, "Administrador");
        var vacio = await servicio.ListarAsync();
        Exigir(vacio.Codigo == CodigoConsultaParqueSolar.Exitoso && vacio.Parques.Count == 0, "lista-vacia");
        await GuardarPaginaAsync<ListadoParquesSolares>(scope.ServiceProvider, salida, "vacio.html");

        foreach (var nombre in new[] { "Zeta req003", "Alfa req003", "Medio req003" })
        {
            var alta = await servicio.CrearAsync(new() { Nombre = nombre, Ubicacion = "Localidad de prueba / " + nombre });
            Exigir(alta.EsExitoso, "alta-administrador-" + nombre);
        }
        contexto.ChangeTracker.Clear();
        var listado = await repositorio.ListarAsync();
        Exigir(listado.Select(parque => parque.Nombre.Valor).SequenceEqual(new[]
            { "Alfa req003", "Medio req003", "Zeta req003" }), "orden-mysql");
        Exigir(contexto.ChangeTracker.Entries().Count() == 0, "listado-sin-seguimiento");
        foreach (var rol in new[] { "Administrador", "Operador", "" })
        {
            Identificar(usuario, rol);
            var resultado = await servicio.ListarAsync();
            Exigir(resultado.Codigo == CodigoConsultaParqueSolar.Exitoso && resultado.Parques.Count == 3,
                "consulta-autenticada-" + (rol == "" ? "sin-roles" : rol));
            foreach (var parque in resultado.Parques)
            {
                var detalle = await servicio.ObtenerPorIdAsync(parque.ID);
                Exigir(detalle.Codigo == CodigoConsultaParqueSolar.Exitoso && detalle.Parque == parque
                    && parque.CapacidadTotalEstimadaMw == 0m, "coincidencia-detalle-y-capacidad");
            }
            if (rol != "Administrador")
                Exigir((await servicio.CrearAsync(new() { Nombre = "Rechazado", Ubicacion = "Prueba" }))
                    .Errores.Single().Codigo == CodigoErrorAltaParqueSolar.AccesoDenegado, "alta-restringida");
        }
        Exigir(contexto.ChangeTracker.Entries().Count() == 0, "detalle-sin-seguimiento");
        foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
        {
            var inexistente = await servicio.ObtenerPorIdAsync(id);
            Exigir(inexistente.Codigo == CodigoConsultaParqueSolar.NoEncontrado && inexistente.Parque is null,
                "id-inexistente");
        }

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-UY");
        await GuardarPaginaAsync<ListadoParquesSolares>(scope.ServiceProvider, salida, "listado.html");
        foreach (var parque in listado)
            await GuardarPaginaAsync<DetalleParqueSolar>(scope.ServiceProvider, salida, $"detalle-{parque.ID:D}.html",
                new Dictionary<string, object?> { ["Id"] = parque.ID });
        await GuardarPaginaAsync<DetalleParqueSolar>(scope.ServiceProvider, salida, "inexistente.html",
            new Dictionary<string, object?> { ["Id"] = Guid.Empty });

        // Provocar un fallo exclusivamente en el destino recién creado, nunca en la base habitual.
        await contexto.Database.ExecuteSqlRawAsync("RENAME TABLE `ParquesSolares` TO `ParquesSolares_req003_respaldo`");
        try
        {
            usuario.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            Exigir((await servicio.ListarAsync()).Codigo == CodigoConsultaParqueSolar.AutenticacionRequerida
                && (await servicio.ObtenerPorIdAsync(Guid.Empty)).Codigo == CodigoConsultaParqueSolar.AutenticacionRequerida,
                "anonimo-rechazado-con-persistencia-no-disponible");
            Identificar(usuario, "Operador");
            var propago = false;
            try { await servicio.ListarAsync(); } catch (MySqlException) { propago = true; }
            Exigir(propago, "fallo-listado-propagado");
            propago = false;
            try { await servicio.ObtenerPorIdAsync(Guid.Empty); } catch (MySqlException) { propago = true; }
            Exigir(propago, "fallo-detalle-propagado");
            await GuardarPaginaAsync<ListadoParquesSolares>(scope.ServiceProvider, salida, "error-listado.html");
            await GuardarPaginaAsync<DetalleParqueSolar>(scope.ServiceProvider, salida, "error-detalle.html",
                new Dictionary<string, object?> { ["Id"] = Guid.Empty });
            foreach (var nombre in new[] { "error-listado.html", "error-detalle.html" })
            {
                var html = System.Net.WebUtility.HtmlDecode(await File.ReadAllTextAsync(Path.Combine(salida, nombre)));
                Exigir(html.Contains("No se pudo cargar la información. Intentá nuevamente") && html.Contains("Recargar")
                    && !html.Contains("No hay parques") && !html.Contains("No se encontró")
                    && !html.Contains("SELECT", StringComparison.OrdinalIgnoreCase), "error-controlado-" + nombre);
            }
        }
        finally
        {
            await contexto.Database.ExecuteSqlRawAsync("RENAME TABLE `ParquesSolares_req003_respaldo` TO `ParquesSolares`");
        }
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        foreach (var consulta in new Func<Task>[] { async () => { await servicio.ListarAsync(cancelacion.Token); },
            async () => { await servicio.ObtenerPorIdAsync(Guid.Empty, cancelacion.Token); } })
        {
            var cancelo = false;
            try { await consulta(); } catch (OperationCanceledException) { cancelo = true; }
            Exigir(cancelo, "cancelacion-propagada");
        }
        Exigir(await contexto.ParquesSolares.CountAsync() == 3, "consultas-no-crean-registros");
        Console.WriteLine("Correcto: integración real de consultas y renderizado; OIDC y autorización SSR no simulados como aceptación.");
    }

    private static void Identificar(IHttpContextAccessor usuario, string rol)
    {
        var identidad = new ClaimsIdentity("req003-integracion-local");
        if (rol != "") identidad.AddClaim(new Claim(ClaimTypes.Role, rol));
        usuario.HttpContext!.User = new ClaimsPrincipal(identidad);
    }

    private static void Exigir(bool condicion, string caso)
    {
        Console.WriteLine(caso + (condicion ? ": correcto" : ": FALLÓ"));
        if (!condicion) throw new InvalidOperationException("Comprobación funcional fallida.");
    }

    private static async Task GuardarPaginaAsync<T>(IServiceProvider servicios, string salida, string nombre,
        Dictionary<string, object?>? parametros = null) where T : IComponent
    {
        await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parametros ?? []))).ToHtmlString());
        var documento = "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<link rel=\"stylesheet\" href=\"/bootstrap.min.css\"><link rel=\"stylesheet\" href=\"/SolarUY.styles.css\">"
            + "</head><body><main class=\"container py-4\"><p>Verificación local de presentación; sin sesión OIDC.</p>"
            + html + "</main></body></html>";
        await File.WriteAllTextAsync(Path.Combine(salida, nombre), documento);
    }

    private static async Task ServirPreviewAsync()
    {
        var raiz = Directory.GetCurrentDirectory();
        var salida = Path.Combine(raiz, "artifacts", "req003");
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8183/");
        listener.Start();
        Console.WriteLine("Preview local en http://localhost:8183/parques-solares; no acredita OIDC ni autorización SSR.");
        while (true)
        {
            var peticion = await listener.GetContextAsync();
            var ruta = peticion.Request.Url!.AbsolutePath;
            var archivo = ruta switch
            {
                "/parques-solares" => Path.Combine(salida, "listado.html"),
                "/bootstrap.min.css" => Path.Combine(raiz, "SolarUY/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css"),
                "/SolarUY.styles.css" => Path.Combine(raiz, "SolarUY/obj/Debug/net10.0/scopedcss/bundle/SolarUY.styles.css"),
                "/vacio" => Path.Combine(salida, "vacio.html"),
                "/error-listado" => Path.Combine(salida, "error-listado.html"),
                "/error-detalle" => Path.Combine(salida, "error-detalle.html"),
                "/parques-solares/00000000-0000-0000-0000-000000000000" => Path.Combine(salida, "inexistente.html"),
                _ when ruta.StartsWith("/parques-solares/") && Guid.TryParse(ruta[17..], out var id)
                    => Path.Combine(salida, $"detalle-{id:D}.html"),
                _ => ""
            };
            if (!File.Exists(archivo)) peticion.Response.StatusCode = 404;
            else
            {
                peticion.Response.ContentType = archivo.EndsWith(".css") ? "text/css" : "text/html; charset=utf-8";
                var contenido = await File.ReadAllBytesAsync(archivo);
                await peticion.Response.OutputStream.WriteAsync(contenido);
            }
            peticion.Response.Close();
        }
    }

    private sealed class NavegacionLocal : NavigationManager
    {
        public NavegacionLocal() => Initialize("http://localhost:8183/", "http://localhost:8183/parques-solares");
    }
}
