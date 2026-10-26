using Microsoft.EntityFrameworkCore;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Domain.Nodos;
using MySql.EntityFrameworkCore.Extensions;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class SolarUyDbContext : DbContext
{
    public SolarUyDbContext(DbContextOptions<SolarUyDbContext> options) : base(options) { }

    public DbSet<ParqueSolar> ParquesSolares => Set<ParqueSolar>();
    public DbSet<Nodo> Nodos => Set<Nodo>();
    public DbSet<Inversor> Inversores => Set<Inversor>();
    public DbSet<Sensor> Sensores => Set<Sensor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCharSet("utf8mb4");
        modelBuilder.ApplyConfiguration(new ParqueSolarConfiguration());
        modelBuilder.ApplyConfiguration(new NodoConfiguration());
        modelBuilder.ApplyConfiguration(new InversorConfiguration());
        modelBuilder.ApplyConfiguration(new SensorConfiguration());
    }
}
