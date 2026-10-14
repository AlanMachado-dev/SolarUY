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
        builder.Property(parque => parque.ID).ValueGeneratedNever();
        builder.Property(parque => parque.Ubicacion).IsRequired();
        builder.Ignore(parque => parque.CapacidadTotalEstimadaMw);
        builder.OwnsOne(parque => parque.Nombre, nombre =>
        {
            nombre.Property(valor => valor.Valor).HasColumnName("Nombre").IsRequired();
            nombre.Property(valor => valor.Clave).HasColumnName("ClaveNombre")
                .UseCollation("BINARY").IsRequired();
            nombre.HasIndex(valor => valor.Clave).IsUnique().HasDatabaseName("UX_ParquesSolares_ClaveNombre");
        });
        builder.Navigation(parque => parque.Nombre).IsRequired();
    }
}
