using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Nodos;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class InversorConfiguration : IEntityTypeConfiguration<Inversor>
{
    public void Configure(EntityTypeBuilder<Inversor> builder)
    {
        builder.ToTable("Inversores");
        builder.HasKey(inversor => inversor.ID);
        builder.Property(inversor => inversor.ID).HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property(inversor => inversor.Nombre).HasColumnType("longtext").IsRequired();
        builder.Property(inversor => inversor.NumeroSerie).HasColumnType("varchar(255)")
            .HasMaxLength(255).UseCollation("utf8mb4_0900_bin").IsRequired();
        builder.HasIndex(inversor => inversor.NumeroSerie).IsUnique().HasDatabaseName("UX_Inversores_NumeroSerie");
        builder.Property<Guid>("IdNodo").HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength();
        builder.HasOne(inversor => inversor.Sensor).WithOne(sensor => sensor.Inversor)
            .HasForeignKey<Sensor>("IdInversor").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(inversor => inversor.Sensor).IsRequired();
    }
}
