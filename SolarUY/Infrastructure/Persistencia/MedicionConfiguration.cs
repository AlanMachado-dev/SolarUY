using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class MedicionConfiguration : IEntityTypeConfiguration<Medicion>
{
    public void Configure(EntityTypeBuilder<Medicion> builder)
    {
        builder.ToTable("Medicion");
        builder.HasKey(medicion => medicion.ID);
        builder.Property(medicion => medicion.ID)
            .HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property<Guid>("IdPanel")
            .HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength();
        builder.HasOne(medicion => medicion.Panel).WithMany()
            .HasForeignKey("IdPanel").IsRequired();
        builder.Property(medicion => medicion.FechaUtc)
            .HasConversion(fecha => fecha.Ticks, ticks => new DateTime(ticks, DateTimeKind.Utc));
        builder.Property(medicion => medicion.Estado).HasConversion<int>();
        builder.HasIndex("IdPanel", nameof(Medicion.FechaUtc), nameof(Medicion.ID))
            .IsDescending(false, true, true);
    }
}
