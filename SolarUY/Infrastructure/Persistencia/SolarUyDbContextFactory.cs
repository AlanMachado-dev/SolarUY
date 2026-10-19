using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class SolarUyDbContextFactory : IDesignTimeDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext(string[] args)
    {
        var configuracion = new ConfigurationBuilder();
        PersistenciaConfiguration.CargarConfiguracion(configuracion,
            PersistenciaConfiguration.ResolverDirectorioProyecto(),
            PersistenciaConfiguration.ObtenerEntorno(args), args);
        var conexion = PersistenciaConfiguration.ObtenerConexion(configuracion.Build());
        var options = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(options, conexion);
        return new SolarUyDbContext(options.Options);
    }
}
