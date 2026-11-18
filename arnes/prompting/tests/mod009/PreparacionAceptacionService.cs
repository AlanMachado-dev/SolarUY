using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;

/// <summary>Datos sintéticos para la aplicación real; no emite sesiones ni se registra en SolarUY.</summary>
static class PreparacionAceptacionService
{
    private const string NombreParque = "Mod009 - Datos sintéticos";
    private const string Ubicacion = "Prueba aislada de claridad de respuestas";

    public static async Task EjecutarAsync(bool preparar)
    {
        var privada = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(".solaruy-acceptance/mod009/host.private.json").Build();
        var destino = new MySqlConnectionStringBuilder(privada.GetConnectionString("SolarUy"));
        var habitual = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("SolarUY/appsettings.Local.json").Build();
        var baseHabitual = new MySqlConnectionStringBuilder(habitual.GetConnectionString("SolarUy"));
        if (destino.Server != "127.0.0.1" || destino.Port != 43329 || destino.Database != "solaruy_mod009"
            || string.Equals(destino.Database, baseHabitual.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El destino debe ser exclusivamente la aceptación aislada mod009.");
        var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opciones, destino.ConnectionString);
        await using var db = new SolarUyDbContext(opciones.Options);
        if (preparar) await db.Database.MigrateAsync();
        var parque = await db.ParquesSolares.Include(p => p.Nodos).ThenInclude(n => n.Paneles)
            .SingleOrDefaultAsync(p => p.Nombre.Valor == NombreParque);
        if (parque is null)
        {
            if (!preparar) throw new InvalidOperationException("Primero debe prepararse el escenario aislado.");
            parque = ParqueSolar.Crear(NombreParque, Ubicacion);
            CrearNodo(parque, "Mod009 Norte", ["Mod009 principal", "Mod009 parcial", "Mod009 cero", "Mod009 pequeño",
                "Mod009 antiguo", "Mod009 sin datos", "Mod009 compartido"]);
            CrearNodo(parque, "Mod009 Sur", ["Mod009 comparación", "Mod009 compartido"]);
            db.Add(parque);
            await db.SaveChangesAsync();
        }
        if (parque.Ubicacion != Ubicacion || parque.Nodos.Count != 2)
            throw new InvalidOperationException("El escenario no coincide con los datos sintéticos esperados.");
        var norte = parque.Nodos.Single(n => n.Nombre == "Mod009 Norte");
        var sur = parque.Nodos.Single(n => n.Nombre == "Mod009 Sur");
        var principal = norte.Paneles.Single(p => p.Nombre.Valor == "Mod009 principal");
        var parcial = norte.Paneles.Single(p => p.Nombre.Valor == "Mod009 parcial");
        var cero = norte.Paneles.Single(p => p.Nombre.Valor == "Mod009 cero");
        var pequeno = norte.Paneles.Single(p => p.Nombre.Valor == "Mod009 pequeño");
        var antiguo = norte.Paneles.Single(p => p.Nombre.Valor == "Mod009 antiguo");
        var sinDatos = norte.Paneles.Single(p => p.Nombre.Valor == "Mod009 sin datos");
        var comparacion = sur.Paneles.Single(p => p.Nombre.Valor == "Mod009 comparación");
        var propios = new[] { principal, parcial, cero, pequeno, antiguo, sinDatos, comparacion }.Select(p => p.ID).ToArray();
        var instante = DateTimeOffset.UtcNow;
        await using var transaccion = await db.Database.BeginTransactionAsync();
        // Solo reemplaza lecturas de los siete paneles identificados del parque sintético, en la base aislada validada.
        foreach (var propio in propios)
        {
            var lecturasPropias = await db.Set<Medicion>()
                .Where(m => EF.Property<Guid>(m, "IdPanel") == propio).ToArrayAsync();
            db.RemoveRange(lecturasPropias);
        }
        await db.SaveChangesAsync();
        void Lectura(Panel panel, DateTimeOffset fecha, float potencia, EstadoPanel estado = EstadoPanel.Operativo) =>
            db.Add(Medicion.Crear(panel, fecha.UtcDateTime, potencia, estado));
        for (var i = 0; i < 6; i++)
        {
            var fecha = instante.AddSeconds(-100 + i * 20);
            Lectura(principal, fecha, 200.25f);
            Lectura(cero, fecha, 0);
            Lectura(pequeno, fecha, 0.004f);
        }
        Lectura(parcial, instante.AddSeconds(-5), 168.36876f);
        Lectura(antiguo, instante.AddMinutes(-5), 40, EstadoPanel.EnFalla);
        Lectura(comparacion, instante.AddSeconds(-5), 300, EstadoPanel.EnFalla);
        Lectura(principal, instante.AddDays(-1), 100);
        Lectura(comparacion, instante.AddDays(-1), 300);
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();
        Directory.CreateDirectory("artifacts/mod009");
        await File.WriteAllTextAsync("artifacts/mod009/oidc-datos.json", JsonSerializer.Serialize(new
        {
            Tipo = "datos_sinteticos_para_OIDC_real", InstanteUtc = instante,
            Destino = "solaruy_mod009:43329", Parque = parque.ID, Principal = principal.ID,
            Parcial = parcial.ID, Cero = cero.ID, Pequeno = pequeno.ID, Antiguo = antiguo.ID, SinDatos = sinDatos.ID,
            PromedioPrincipalEsperadoW = 200.25m, PotenciaPequenaEsperada = "menos de 0,01 W",
            Nota = "Promedio constante mientras haya muestras. Refrescar después de autenticar para consultar períodos recientes sin activar el sensor."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Datos sintéticos mod009 preparados/actualizados únicamente en solaruy_mod009:43329. Sin modificar la base habitual.");
    }

    private static void CrearNodo(ParqueSolar parque, string nombre, string[] paneles)
    {
        var nodo = parque.AgregarNodo(nombre, 0, 0,
            Inversor.Crear("Simulado", Guid.NewGuid().ToString(), Sensor.Crear("Simulado", Guid.NewGuid().ToString())));
        foreach (var panel in paneles) nodo.AgregarPanel(panel, "Simulado mod009", 500, DateTime.UtcNow.AddDays(-90));
    }
}
