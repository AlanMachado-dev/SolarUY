using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.ParquesSolares;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class ParqueSolarConfiguration : IEntityTypeConfiguration<ParqueSolar>
{
    public void Configure(EntityTypeBuilder<ParqueSolar> builder)
    {
        builder.ToTable("ParquesSolares");
        builder.HasKey(parque => parque.ID);
        builder.Property(parque => parque.ID).HasConversion(
            id => id.ToString("D"), texto => Guid.Parse(texto))
            // El proveedor reserva el nombre exacto char(36) para su mapeo Guid nativo.
            // Tipo base + longitud preservan nuestra conversión explícita Guid <-> string.
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property(parque => parque.Ubicacion).HasColumnType("longtext").IsRequired();
        builder.Ignore(parque => parque.CapacidadTotalEstimadaMw);
        builder.OwnsOne(parque => parque.Nombre, nombre =>
        {
            nombre.Property<Guid>("ParqueSolarID").HasConversion(
                id => id.ToString("D"), texto => Guid.Parse(texto))
                .HasColumnType("char").HasMaxLength(36).IsFixedLength();
            nombre.Property(valor => valor.Valor).HasColumnName("Nombre")
                .HasMaxLength(100).HasColumnType("varchar(100)").IsRequired();
            nombre.Property(valor => valor.Clave).HasColumnName("ClaveNombre")
                .HasMaxLength(100).HasColumnType("varchar(100)")
                .UseCollation("utf8mb4_0900_bin").IsRequired();
            nombre.HasIndex(valor => valor.Clave).IsUnique().HasDatabaseName("UX_ParquesSolares_ClaveNombre");
        });
        builder.Navigation(parque => parque.Nombre).IsRequired();
    }
}
