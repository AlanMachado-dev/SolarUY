using Microsoft.EntityFrameworkCore;
using SolarUY.Domain.ParquesSolares;
using MySql.EntityFrameworkCore.Extensions;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class SolarUyDbContext : DbContext
{
    public SolarUyDbContext(DbContextOptions<SolarUyDbContext> options) : base(options) { }

    public DbSet<ParqueSolar> ParquesSolares => Set<ParqueSolar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCharSet("utf8mb4");
        modelBuilder.ApplyConfiguration(new ParqueSolarConfiguration());
    }
}
