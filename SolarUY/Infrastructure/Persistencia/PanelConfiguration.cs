using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class PanelConfiguration : IEntityTypeConfiguration<Panel>
{
    public void Configure(EntityTypeBuilder<Panel> builder)
    {
        builder.ToTable("Paneles");
        builder.HasKey(panel => panel.ID);
        builder.Property(panel => panel.ID).HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property<Guid>("IdNodo").HasColumnName("IdNodo")
            .HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength();
        builder.HasOne(panel => panel.Nodo).WithMany(nodo => nodo.Paneles)
            .HasForeignKey("IdNodo").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Property(panel => panel.Modelo).HasColumnType("longtext").IsRequired();
        builder.Property(panel => panel.PotenciaNominalW).IsRequired();
        builder.Property(panel => panel.FechaInstalacion).HasColumnType("datetime(0)")
            .HasConversion(fecha => fecha.ToUniversalTime(), fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
        builder.Property(panel => panel.Estado).HasConversion<int>().IsRequired();
        builder.Ignore(panel => panel.GeneracionActualW);
        builder.Ignore(panel => panel.UltimoMantenimiento);
        builder.OwnsOne(panel => panel.Nombre, nombre =>
        {
            nombre.Property<Guid>("PanelID").HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
                .HasColumnType("char").HasMaxLength(36).IsFixedLength();
            // El índice del objeto poseído comparte IdNodo con la FK de su propietario.
            nombre.Property<Guid>("IdNodo").HasColumnName("IdNodo")
                .HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
                .HasColumnType("char").HasMaxLength(36).IsFixedLength();
            nombre.Property(valor => valor.Valor).HasColumnName("Nombre")
                .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            nombre.Property(valor => valor.Clave).HasColumnName("ClaveNombre")
                .HasColumnType("varchar(100)").HasMaxLength(100).UseCollation("utf8mb4_0900_bin").IsRequired();
            nombre.HasIndex("IdNodo", nameof(NombrePanel.Clave)).IsUnique()
                .HasDatabaseName("UX_Paneles_IdNodo_ClaveNombre");
        });
        builder.Navigation(panel => panel.Nombre).IsRequired();
    }
}
