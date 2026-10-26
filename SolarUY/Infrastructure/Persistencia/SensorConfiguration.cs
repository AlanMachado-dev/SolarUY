using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Nodos;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("Sensores");
        builder.HasKey(sensor => sensor.ID);
        builder.Property(sensor => sensor.ID).HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property(sensor => sensor.Nombre).HasColumnType("longtext").IsRequired();
        builder.Property(sensor => sensor.NumeroSerie).HasColumnType("varchar(255)")
            .HasMaxLength(255).UseCollation("utf8mb4_0900_bin").IsRequired();
        builder.HasIndex(sensor => sensor.NumeroSerie).IsUnique().HasDatabaseName("UX_Sensores_NumeroSerie");
        builder.Property(sensor => sensor.Endpoint).HasColumnType("longtext").IsRequired(false);
        builder.Property<Guid>("IdInversor").HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength();
    }
}
