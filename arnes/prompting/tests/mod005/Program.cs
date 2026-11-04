using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.Nodos.Servicios;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Monitoreo;

try
{
    var configuracion = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("SolarUY"))
        .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
        .AddEnvironmentVariables().Build();
    var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
    var destino = $"solaruy_mod005_{Guid.NewGuid():N}";
    Exigir(!destino.Equals(conexion.Database, StringComparison.OrdinalIgnoreCase), "destino aislado");
    conexion.Database = destino;
    var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
    PersistenciaConfiguration.ConfigurarContexto(opciones, conexion.ConnectionString);
    await using var contexto = new SolarUyDbContext(opciones.Options);
    Console.WriteLine($"Destino aislado: {destino}");
    try
    {
        await contexto.Database.MigrateAsync();
        var parque = ParqueSolar.Crear("Parque mod005", "Sur");
        var nodo = parque.AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")));
        var panelCero = nodo.AgregarPanel("Cero", "Modelo", 500, DateTime.UtcNow);
        var panelVacio = nodo.AgregarPanel("Vacío", "Modelo", 500, DateTime.UtcNow);
        var panelEmpate = nodo.AgregarPanel("Empate", "Modelo", 500, DateTime.UtcNow);
        var instante = DateTime.UtcNow;
        var empatadas = new[]
        {
            Medicion.Crear(panelEmpate, instante, 800, EstadoPanel.Operativo),
            Medicion.Crear(panelEmpate, instante, 900, EstadoPanel.Operativo)
        };
        var esperada = empatadas.MaxBy(medicion => medicion.ID)!;
        contexto.Add(parque);
        contexto.AddRange(empatadas);
        contexto.AddRange(
            Medicion.Crear(panelCero, instante.AddSeconds(-1), 400, EstadoPanel.Operativo),
            Medicion.Crear(panelCero, instante, 0, EstadoPanel.EnFalla));
        await contexto.SaveChangesAsync();

        var repositorio = new MedicionRepository(new FabricaContexto(opciones.Options));
        var nodos = new NodoService(new NodoRepository(contexto), new ParqueSolarRepository(contexto),
            new Operador(), repositorio);
        var detalle = await nodos.ObtenerPorIdAsync(nodo.ID);
        var filas = detalle.Paneles.ToDictionary(panel => panel.ID);
        Exigir(filas.Count == 3 && filas[panelVacio.ID].UltimaMedicion is null &&
            filas[panelCero.ID].UltimaMedicion is
                { GeneracionActualW: 0, Estado: EstadoPanel.EnFalla, FechaUtc.Kind: DateTimeKind.Utc },
            "asociación de filas, lectura de 0 W, estado, UTC y ausencia");
        var monitoreo = new MonitoreoService(repositorio, new NotificacionMedicion());
        var individual = await monitoreo.ConsultarAsync(panelEmpate.ID);
        Exigir(individual?.MedicionId == esperada.ID &&
            filas[panelEmpate.ID].UltimaMedicion?.GeneracionActualW == esperada.GeneracionActualW,
            "desempate por ID en lote e individual");
        var subconjunto = await repositorio.ObtenerUltimasAsync([panelCero.ID, panelVacio.ID], default);
        Exigir(subconjunto.Count == 1 && subconjunto.ContainsKey(panelCero.ID), "filtro de paneles");
        Exigir((await repositorio.ObtenerUltimasAsync([], default)).Count == 0, "solicitud vacía");

        if (args.Contains("--servir"))
        {
            await ServirAsync(conexion.ConnectionString, nodo.ID, panelEmpate.ID, panelVacio.ID);
            var muestras = await contexto.Set<Medicion>().AsNoTracking()
                .Where(medicion => EF.Property<Guid>(medicion, "IdPanel") == panelEmpate.ID &&
                    medicion.FechaUtc > instante)
                .OrderBy(medicion => medicion.FechaUtc).Select(medicion => medicion.FechaUtc).ToListAsync();
            Console.WriteLine($"Nuevas mediciones persistidas para Empate: {muestras.Count}");
            foreach (var intervalo in muestras.Zip(muestras.Skip(1), (previa, siguiente) => siguiente - previa))
                Console.WriteLine(FormattableString.Invariant($"Intervalo entre guardados: {intervalo.TotalSeconds:F2} s"));
        }
    }
    finally
    {
        await contexto.Database.EnsureDeletedAsync();
    }
}
catch (Exception error)
{
    Console.Error.WriteLine($"Verificación fallida ({error.GetBaseException().GetType().Name}).");
    return 1;
}

return 0;

static void Exigir(bool condicion, string criterio)
{
    Console.WriteLine($"{(condicion ? "OK" : "FALLO")} {criterio}");
    if (!condicion)
        throw new InvalidOperationException();
}

static async Task ServirAsync(string conexion, Guid nodoId, Guid panelId, Guid panelVacioId)
{
    const string origen = "https://localhost:8185";
    var ensamblado = Path.GetFullPath("artifacts/mod005/compilacion/bin/SolarUY/debug/SolarUY.dll");
    var inicio = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = Path.GetFullPath("SolarUY"),
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var argumento in new[] { ensamblado, "--urls", origen })
        inicio.ArgumentList.Add(argumento);
    inicio.Environment["ConnectionStrings__SolarUy"] = conexion;
    inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    using var proceso = Process.Start(inicio) ?? throw new InvalidOperationException();
    proceso.BeginOutputReadLine();
    proceso.BeginErrorReadLine();
    try
    {
        Console.WriteLine($"{origen}/nodos/{nodoId:D}");
        Console.WriteLine($"{origen}/paneles/{panelId:D}/monitoreo");
        Console.WriteLine($"{origen}/paneles/{panelVacioId:D}/monitoreo");
        Console.WriteLine("Presioná Enter para finalizar y retirar la base aislada.");
        await Task.WhenAny(Task.Run(Console.ReadLine), proceso.WaitForExitAsync(), Task.Delay(TimeSpan.FromMinutes(10)));
        Exigir(!proceso.HasExited, "instancia de aceptación activa");
    }
    finally
    {
        if (!proceso.HasExited)
            proceso.Kill(entireProcessTree: true);
        await proceso.WaitForExitAsync();
    }
}

sealed class FabricaContexto(DbContextOptions<SolarUyDbContext> opciones) : IDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext() => new(opciones);
}

sealed class Operador : IUsuarioActualService
{
    public bool EstaAutenticado => true;
    public bool PerteneceAlRol(string rol) => rol == "Operador";
}
