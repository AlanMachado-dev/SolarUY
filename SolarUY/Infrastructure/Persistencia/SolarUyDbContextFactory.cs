using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class SolarUyDbContextFactory : IDesignTimeDbContextFactory<SolarUyDbContext>
{
    public SolarUyDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__SolarUy")
            ?? throw new InvalidOperationException("Configurá ConnectionStrings__SolarUy para ejecutar las migraciones.");
        var options = new DbContextOptionsBuilder<SolarUyDbContext>().UseSqlite(conexion).Options;
        return new SolarUyDbContext(options);
    }
}
