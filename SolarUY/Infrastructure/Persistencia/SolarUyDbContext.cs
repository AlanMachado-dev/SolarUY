using Microsoft.EntityFrameworkCore;
using SolarUY.Domain.ParquesSolares;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class SolarUyDbContext : DbContext
{
    public SolarUyDbContext(DbContextOptions<SolarUyDbContext> options) : base(options) { }

    public DbSet<ParqueSolar> ParquesSolares => Set<ParqueSolar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ParqueSolarConfiguration());
    }
}
