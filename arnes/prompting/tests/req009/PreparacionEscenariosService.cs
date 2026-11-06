using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;

// Herramienta funcional de datos simulados; no se registra en la aplicación ni expone rutas.
static class PreparacionEscenariosService
{
    public static string ObtenerConexion()
    {
        var configuracion = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(".solaruy-acceptance/req009/host.private.json").Build();
        var conexion = new MySqlConnectionStringBuilder(configuracion.GetConnectionString("SolarUy"));
        if (conexion.Server != "127.0.0.1" || conexion.Port != 43319 || conexion.Database != "solaruy_req009")
            throw new InvalidOperationException("Solo se admite el destino aislado req009.");
        var habitual = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("SolarUY/appsettings.Local.json").Build();
        if (new MySqlConnectionStringBuilder(habitual.GetConnectionString("SolarUy")).Database == conexion.Database)
            throw new InvalidOperationException("No se admite la base habitual.");
        return conexion.ConnectionString;
    }

    public static async Task EjecutarAsync(bool actualizar)
    {
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, ObtenerConexion());
        await using var contexto = new SolarUyDbContext(opciones.Options);
        var parques = await contexto.ParquesSolares.Include(p => p.Nodos).ThenInclude(n => n.Paneles).ToArrayAsync();
        var volumen = parques.SingleOrDefault(p => p.Nombre.Valor == "Volumen req009");
        if (actualizar)
        {
            if (volumen is null) throw new InvalidOperationException("Primero preparar los escenarios.");
            foreach (var nodo in volumen.Nodos)
                foreach (var panel in nodo.Paneles)
                    contexto.Add(Medicion.Crear(panel, DateTime.UtcNow,
                        nodo.Nombre == "Comparación A" ? 10 : 20,
                        nodo.Nombre == "Comparación A" ? EstadoPanel.EnFalla : EstadoPanel.Operativo));
            await contexto.SaveChangesAsync();
            Console.WriteLine("OK: nuevas mediciones simuladas guardadas solo en Volumen req009.");
            return;
        }
        if (volumen is not null) throw new InvalidOperationException("Escenarios ya preparados; no se duplican muestras.");
        var norte = parques.Single(p => p.Nombre.Valor == "Req009 Norte");
        var nodoNorte = norte.Nodos.Single();
        var panelNorte = nodoNorte.Paneles.Single(p => p.Nombre.Valor == "Panel 3");
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zona).Date;
        DateTime Utc(int dias, int hora, int minuto = 0) => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(hoy.AddDays(dias).AddHours(hora).AddMinutes(minuto), DateTimeKind.Unspecified), zona);
        if (DateTime.UtcNow < Utc(0, 12)) throw new InvalidOperationException("La preparación requiere después de la mañana local.");
        contexto.Add(Medicion.Crear(panelNorte, Utc(0, 6), 0, EstadoPanel.Operativo));
        contexto.Add(Medicion.Crear(panelNorte, Utc(0, 8), 100, EstadoPanel.Operativo));
        contexto.Add(Medicion.Crear(panelNorte, Utc(0, 11, 59), 50, EstadoPanel.Operativo));
        var cero = nodoNorte.AgregarPanel("Cero comprobado", "Simulado", 500, DateTime.UtcNow.AddDays(-90));
        contexto.Add(cero);
        contexto.Add(Medicion.Crear(cero, Utc(0, 6), 0, EstadoPanel.Operativo));
        contexto.Add(Medicion.Crear(cero, Utc(0, 7), 0, EstadoPanel.Operativo));
        volumen = ParqueSolar.Crear("Volumen req009", "Datos simulados de aceptación req009");
        foreach (var nombre in new[] { "Comparación A", "Comparación B" })
        {
            var nodo = volumen.AgregarNodo(nombre, 0, 0,
                Inversor.Crear("Simulado", Guid.NewGuid().ToString(), Sensor.Crear("Simulado", Guid.NewGuid().ToString())));
            for (var i = 0; i < 60; i++)
            {
                var panel = nodo.AgregarPanel("Volumen " + i, "Simulado", 500, DateTime.UtcNow.AddDays(-90));
                contexto.Add(Medicion.Crear(panel, Utc(-1, 9), 300, EstadoPanel.Operativo));
                contexto.Add(Medicion.Crear(panel, Utc(0, 9), nombre.EndsWith('A') ? 100 : 400, EstadoPanel.Operativo));
                if (i % 2 == 0) contexto.Add(Medicion.Crear(panel, Utc(0, 9, 20), nombre.EndsWith('A') ? 100 : 400, EstadoPanel.Operativo));
            }
        }
        contexto.Add(volumen);
        await contexto.SaveChangesAsync();
        Directory.CreateDirectory("artifacts/req009");
        await File.WriteAllTextAsync("artifacts/req009/escenarios-datos.json", JsonSerializer.Serialize(new
        {
            FechaUtc = DateTimeOffset.UtcNow, Destino = "solaruy_req009:43319", Tipo = "datos_simulados",
            VolumenPaneles = 120, PromedioObservadoNodoAHoyW = 6000, PromedioObservadoNodoAAyerW = 18000,
            PromedioObservadoNodoBHoyW = 24000, PromedioObservadoNodoBAyerW = 18000,
            PanelNorteMananaW = 50, CeroComprobadoW = 0, Cobertura = "Parcial; no declarar ganador concluyente",
            PanelNorte = panelNorte.ID, PanelCero = cero.ID,
            Nodos = volumen.Nodos.Select(n => new { n.ID, n.Nombre })
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("OK: escenarios aislados preparados; 120 paneles de volumen, mañana y cero medido. Evidencia depurada en artifacts/req009/escenarios-datos.json.");
    }
}
