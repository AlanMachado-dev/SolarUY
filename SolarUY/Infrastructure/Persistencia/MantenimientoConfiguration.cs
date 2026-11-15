using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class MantenimientoConfiguration : IEntityTypeConfiguration<Mantenimiento>
{
    public void Configure(EntityTypeBuilder<Mantenimiento> builder)
    {
        builder.ToTable("Mantenimientos");
        builder.HasKey(mantenimiento => mantenimiento.ID);
        builder.Property(mantenimiento => mantenimiento.ID)
            .HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property<Guid>("IdPanel")
            .HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength();
        builder.HasOne(mantenimiento => mantenimiento.Panel).WithMany(panel => panel.Mantenimientos)
            .HasForeignKey("IdPanel").IsRequired();
        builder.Metadata.FindNavigation(nameof(Mantenimiento.Panel))!.Inverse!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(mantenimiento => mantenimiento.Tipo).IsRequired();
        builder.Property(mantenimiento => mantenimiento.Responsable).IsRequired();
        builder.Property(mantenimiento => mantenimiento.FechaProgramada)
            .HasConversion(fecha => fecha.Ticks, ticks => new DateTime(ticks, DateTimeKind.Utc));
        builder.Property(mantenimiento => mantenimiento.InicioProcesadoUtc)
            .HasConversion(fecha => fecha!.Value.Ticks, ticks => new DateTime(ticks, DateTimeKind.Utc));
        builder.Property(mantenimiento => mantenimiento.FechaRealizada)
            .HasConversion(fecha => fecha!.Value.Ticks, ticks => new DateTime(ticks, DateTimeKind.Utc));
        builder.HasIndex(mantenimiento => new { mantenimiento.InicioProcesadoUtc, mantenimiento.FechaRealizada,
            mantenimiento.FechaProgramada });
    }
}
