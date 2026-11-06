using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Balanceo;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;
using SolarUY.Presentation.Monitoreo;
using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

// Herramienta funcional externa. Nunca se incorpora al servidor ni habilita accesos de prueba en él.
try
{
    await EjecutarAsync(args);
}
catch (Exception error)
{
    Console.Error.WriteLine($"Verificación detenida. Tipo: {error.GetType().Name}. No se imprimen detalles privados.");
    if (error is MySqlException mysql)
    {
        Console.Error.WriteLine($"Código MySQL: {mysql.Number}; SQLSTATE: {mysql.SqlState}.");
        foreach (var categoria in new[] { "public key", "authentication", "connect", "SSL", "Access denied" })
            if (mysql.Message.Contains(categoria, StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine($"Categoría: {categoria}.");
        if (mysql.InnerException is not null)
            Console.Error.WriteLine($"Causa: {mysql.InnerException.GetType().Name}.");
    }
    Environment.ExitCode = 1;
}

static async Task EjecutarAsync(string[] argumentos)
{
    if (!File.Exists("SolarUY.slnx") || argumentos.Length != 1)
        throw new InvalidOperationException("Ejecutar desde la raíz con preparar o sesiones.");
    var configuracion = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(".solaruy-acceptance/host.json", optional: false).Build();
    var conexion = new MySqlConnectionStringBuilder(configuracion.GetConnectionString("SolarUy"));
    if (conexion.Database != "solaruy_req008" || conexion.Server != "127.0.0.1" || conexion.Port != 13308)
        throw new InvalidOperationException("Destino de aceptación no reconocido.");
    var habitual = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("SolarUY/appsettings.Local.json").Build();
    if (new MySqlConnectionStringBuilder(habitual.GetConnectionString("SolarUy")).Database == conexion.Database)
        throw new InvalidOperationException("El esquema de prueba coincide con el habitual.");

    if (argumentos[0] == "bloqueo-no-reintento")
    {
        await VerificarNoReintentoAsync(conexion);
        return;
    }

    if (argumentos[0] is "bloquear-escritura" or "restaurar-escritura")
    {
        conexion.UserID = "root";
        conexion.Password = File.ReadAllText(".solaruy-acceptance/mysql-root-password.txt").Trim();
        await using var administrador = new MySqlConnection(conexion.ConnectionString);
        await administrador.OpenAsync();
        var sql = argumentos[0] == "bloquear-escritura"
            ? "CREATE TRIGGER solaruy_req008.req008_rechazar_medicion BEFORE INSERT ON solaruy_req008.Medicion FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='req008_fallo_controlado'"
            : "DROP TRIGGER IF EXISTS solaruy_req008.req008_rechazar_medicion";
        await using var comando = new MySqlCommand(sql, administrador);
        await comando.ExecuteNonQueryAsync();
        Console.WriteLine("Fallo controlado de INSERT actualizado únicamente en solaruy_req008.");
        return;
    }
    if (argumentos[0] is "atrasada" or "sincronizacion")
    {
        using var datos = JsonDocument.Parse(File.ReadAllText(".solaruy-acceptance/datos.json"));
        var panelId = datos.RootElement.GetProperty("Paneles")[0].GetGuid();
        if (argumentos[0] == "sincronizacion")
        {
            await VerificarSincronizacionAsync(conexion.ConnectionString, panelId);
            return;
        }
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
        await using var contexto = new SolarUyDbContext(opciones.Options);
        var panel = await contexto.Paneles.SingleAsync(p => p.ID == panelId);
        contexto.Add(Medicion.Crear(panel, DateTime.UtcNow.AddDays(-1), 12, EstadoPanel.Operativo));
        await contexto.SaveChangesAsync();
        Console.WriteLine("Lectura atrasada insertada exclusivamente en la base aislada.");
        return;
    }
    if (argumentos[0] == "longpoll")
    {
        using var entrada = JsonDocument.Parse(File.ReadAllText(".solaruy-acceptance/longpoll.json"));
        var raiz = entrada.RootElement;
        var origen = new Uri(raiz.GetProperty("origen").GetString()!);
        if (origen.Scheme != "https" || origen.Host != "localhost")
            throw new InvalidOperationException("Origen de prueba no reconocido.");
        using var certificadoServidor = X509CertificateLoader.LoadCertificateFromFile(".solaruy-private/localhost.crt");
        var cookies = new CookieContainer();
        foreach (var cookie in raiz.GetProperty("cookies").EnumerateArray())
            cookies.Add(origen, new Cookie(cookie.GetProperty("name").GetString()!, cookie.GetProperty("value").GetString()!, "/") { Secure = true });
        await using var hub = new HubConnectionBuilder().WithUrl(new Uri(origen, "/hubs/monitoreo"), opciones =>
        {
            opciones.Transports = HttpTransportType.LongPolling;
            opciones.Cookies = cookies;
            opciones.HttpMessageHandlerFactory = manejador =>
            {
                if (manejador is HttpClientHandler cliente)
                    cliente.ServerCertificateCustomValidationCallback = (_, certificado, _, _) =>
                        certificado?.GetCertHashString() == certificadoServidor.GetCertHashString();
                return manejador;
            };
        }).Build();
        using var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await hub.StartAsync(plazo.Token);
        await hub.InvokeAsync("Registrar", new RegistrarMedicionDto(raiz.GetProperty("panel").GetGuid(), 345, EstadoPanel.Operativo), plazo.Token);
        await hub.StopAsync(plazo.Token);
        Console.WriteLine("SignalR Long Polling negoció y registró una lectura por el balanceador.");
        return;
    }

    if (argumentos[0] == "preparar")
    {
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
        await using var contexto = new SolarUyDbContext(opciones.Options);
        await contexto.Database.MigrateAsync();
        var parque = await contexto.ParquesSolares.Include(p => p.Nodos).ThenInclude(n => n.Paneles)
            .SingleOrDefaultAsync(p => p.Ubicacion == "Aceptación req008");
        if (parque is null)
        {
            parque = ParqueSolar.Crear("Parque req008", "Aceptación req008");
            var sensor = Sensor.Crear("Sensor req008", "SENSOR-REQ008");
            var inversor = Inversor.Crear("Inversor req008", "INVERSOR-REQ008", sensor);
            var nodo = parque.AgregarNodo("Nodo req008", -34, -54, inversor);
            nodo.AgregarPanel("Panel A", "Modelo req008", 500, DateTime.UtcNow);
            nodo.AgregarPanel("Panel B", "Modelo req008", 600, DateTime.UtcNow);
            contexto.Add(parque);
            await contexto.SaveChangesAsync();
        }
        var datos = new { Parque = parque.ID, Nodo = parque.Nodos.Single().ID,
            Paneles = parque.Nodos.Single().Paneles.OrderBy(p => p.Nombre.Valor).Select(p => p.ID).ToArray() };
        await File.WriteAllTextAsync(".solaruy-acceptance/datos.json", JsonSerializer.Serialize(datos));
        Console.WriteLine("Migraciones y dos paneles preparados exclusivamente en solaruy_req008.");
        return;
    }
    if (argumentos[0] != "sesiones")
        throw new InvalidOperationException("Operación no reconocida.");

    var certificado = X509CertificateLoader.LoadPkcs12FromFile(".solaruy-private/claves.pfx",
        File.ReadAllText(".solaruy-private/claves-password.txt").Trim(), X509KeyStorageFlags.EphemeralKeySet);
    using var servicios = new ServiceCollection().AddLogging(b => b.ClearProviders())
        .AddDataProtection().SetApplicationName("SolarUY")
        .PersistKeysToFileSystem(new DirectoryInfo(".solaruy-acceptance/claves"))
        .ProtectKeysWithCertificate(certificado).Services.BuildServiceProvider();
    var protector = servicios.GetRequiredService<IDataProtectionProvider>().CreateProtector(
        "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", "Cookies", "v2");
    var formato = new TicketDataFormat(protector);
    var sesiones = new Dictionary<string, string>();
    var tipoRol = configuracion["OpenIdConnect:RolesClaimType"] ?? "roles";
    foreach (var rol in new[] { "Administrador", "Operador" })
    {
        var identidad = new ClaimsIdentity([new Claim("name", $"Prueba auxiliar {rol}"), new Claim(tipoRol, rol)],
            "Cookies", "name", tipoRol);
        var propiedades = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            IsPersistent = false, AllowRefresh = false
        };
        sesiones[rol] = formato.Protect(new AuthenticationTicket(new ClaimsPrincipal(identidad), propiedades,
            CookieAuthenticationDefaults.AuthenticationScheme));
    }
    await File.WriteAllTextAsync(".solaruy-acceptance/sesiones.json", JsonSerializer.Serialize(sesiones));
    Console.WriteLine("Sesiones sintéticas auxiliares guardadas en un archivo privado. No constituyen aceptación OIDC real.");
}

static async Task VerificarSincronizacionAsync(string conexion, Guid panelId)
{
    var consultas = new RecuentoConsultasInterceptor();
    var registros = new ServiceCollection();
    registros.AddLogging(b => b.ClearProviders());
    registros.AddDbContextFactory<SolarUyDbContext>(opciones =>
    {
        PersistenciaConfiguration.ConfigurarContexto(opciones, conexion);
        opciones.AddInterceptors(consultas);
    });
    registros.AddScoped<IMedicionRepository, MedicionRepository>();
    registros.AddScoped<MonitoreoService>();
    registros.AddSingleton<NotificacionMedicion>();
    registros.AddSingleton<INotificacionMedicion>(s => s.GetRequiredService<NotificacionMedicion>());
    registros.AddSingleton<IPanelesMonitoreados>(s => s.GetRequiredService<NotificacionMedicion>());
    registros.AddSingleton(new BalanceoOptions { IntervaloSincronizacionSegundos = 0.2 });
    registros.AddSingleton<SincronizacionMedicionesService>();
    await using var servicios = registros.BuildServiceProvider();
    var notificaciones = servicios.GetRequiredService<NotificacionMedicion>();
    var recibidas = 0;
    var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Operador")], "auxiliar"));
    using var suscripcion = notificaciones.Suscribir(usuario, panelId, lectura =>
    {
        Interlocked.Increment(ref recibidas);
        return Task.CompletedTask;
    });
    var trabajador = servicios.GetRequiredService<SincronizacionMedicionesService>();
    await trabajador.StartAsync(CancellationToken.None);
    try
    {
        var limite = DateTime.UtcNow.AddSeconds(4);
        while (Volatile.Read(ref recibidas) == 0 && DateTime.UtcNow < limite)
            await Task.Delay(50);
        if (recibidas != 1) throw new InvalidOperationException("No se recibió la lectura inicial.");
        await Task.Delay(800);
        if (recibidas != 1) throw new InvalidOperationException("Se difundió una lectura duplicada.");
        suscripcion.Dispose();
        await Task.Delay(500);
        var cantidad = consultas.Cantidad;
        await Task.Delay(700);
        if (consultas.Cantidad != cantidad || notificaciones.ObtenerPaneles().Count != 0)
            throw new InvalidOperationException("Persisten consultas sin suscripciones.");
        Console.WriteLine("Sincronizador con MySQL real: sin notificaciones duplicadas ni consultas tras retirar la última suscripción.");
    }
    finally { await trabajador.StopAsync(CancellationToken.None); }
}

static async Task VerificarNoReintentoAsync(MySqlConnectionStringBuilder conexion)
{
    conexion.UserID = "root";
    conexion.Password = File.ReadAllText(".solaruy-acceptance/mysql-root-password.txt").Trim();
    await using var bloqueo = new MySqlConnection(conexion.ConnectionString);
    await using var observacion = new MySqlConnection(conexion.ConnectionString);
    await bloqueo.OpenAsync();
    await observacion.OpenAsync();
    await using var tomar = new MySqlCommand("LOCK TABLES solaruy_req008.ParquesSolares READ", bloqueo);
    await tomar.ExecuteNonQueryAsync();
    var esperando = new List<long>();
    using var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(40));
    try
    {
        await File.WriteAllTextAsync(".solaruy-acceptance/bloqueo-listo", "listo");
        while (esperando.Count == 0)
        {
            plazo.Token.ThrowIfCancellationRequested();
            await using var buscar = new MySqlCommand("SELECT ID FROM information_schema.PROCESSLIST WHERE USER='solaruy' AND DB='solaruy_req008' AND STATE LIKE '%lock%' AND INFO LIKE '%INSERT%'", observacion);
            await using (var lector = await buscar.ExecuteReaderAsync(plazo.Token))
                while (await lector.ReadAsync(plazo.Token)) esperando.Add(lector.GetInt64(0));
            if (esperando.Count == 0) await Task.Delay(100, plazo.Token);
        }
        await File.WriteAllTextAsync(".solaruy-acceptance/escritura-enviada", "confirmada");
        while (!File.Exists(".solaruy-acceptance/liberar-bloqueo"))
            await Task.Delay(100, plazo.Token);
        // Cancelar solo las conexiones de la solicitud inicial bloqueada; un reintento en B no se cancela.
        foreach (var id in esperando)
        {
            try
            {
                await using var cancelar = new MySqlCommand($"KILL CONNECTION {id}", observacion);
                await cancelar.ExecuteNonQueryAsync();
            }
            catch (MySqlException error) when (error.Number == 1094) { }
        }
    }
    finally
    {
        await using var liberar = new MySqlCommand("UNLOCK TABLES", bloqueo);
        await liberar.ExecuteNonQueryAsync();
    }
    using var entrada = JsonDocument.Parse(File.ReadAllText(".solaruy-acceptance/no-reintento.json"));
    await using var contar = new MySqlCommand("SELECT COUNT(*) FROM solaruy_req008.ParquesSolares WHERE Ubicacion=@marca", observacion);
    contar.Parameters.AddWithValue("@marca", entrada.RootElement.GetProperty("marca").GetString());
    await Task.Delay(1000);
    var cantidad = Convert.ToInt64(await contar.ExecuteScalarAsync());
    await File.WriteAllTextAsync(".solaruy-acceptance/no-reintento-resultado.json", JsonSerializer.Serialize(new { Cantidad = cantidad }));
    Console.WriteLine("Control de escrituras no reintentadas completado en la base aislada.");
}
