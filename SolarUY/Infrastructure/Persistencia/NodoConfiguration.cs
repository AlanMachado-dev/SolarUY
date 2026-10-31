using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Nodos;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class NodoConfiguration : IEntityTypeConfiguration<Nodo>
{
    public void Configure(EntityTypeBuilder<Nodo> builder)
    {
        builder.ToTable("Nodos");
        builder.HasKey(nodo => nodo.ID);
        builder.Property(nodo => nodo.ID).HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength().ValueGeneratedNever();
        builder.Property(nodo => nodo.Nombre).HasColumnType("longtext").IsRequired();
        builder.Property(nodo => nodo.Latitud).IsRequired();
        builder.Property(nodo => nodo.Longitud).IsRequired();
        builder.Ignore(nodo => nodo.CantidadPaneles);
        builder.Navigation(nodo => nodo.Paneles).HasField("_paneles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property<Guid>("IdParque").HasConversion(id => id.ToString("D"), texto => Guid.Parse(texto))
            .HasColumnType("char").HasMaxLength(36).IsFixedLength();
        builder.HasOne(nodo => nodo.Parque).WithMany(parque => parque.Nodos)
            .HasForeignKey("IdParque").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(nodo => nodo.Inversor).WithOne(inversor => inversor.Nodo)
            .HasForeignKey<Inversor>("IdNodo").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(nodo => nodo.Inversor).IsRequired();
    }
}
