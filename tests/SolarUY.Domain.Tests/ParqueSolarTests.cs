using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class ParqueSolarTests
{
    [Fact]
    public void Crear_ParqueVacioTieneCapacidadCero()
    {
        Assert.Equal(0m, ParqueSolar.Crear("Sur", "Montevideo").CapacidadTotalEstimadaMw);
    }

    [Fact]
    public void Crear_LimpiaEspaciosExterioresDeUbicacion()
    {
        Assert.Equal("Montevideo", ParqueSolar.Crear("Sur", "  Montevideo  ").Ubicacion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_RechazaUbicacionObligatoria(string? ubicacion)
    {
        var error = Assert.Throws<DatosParqueSolarInvalidosException>(
            () => ParqueSolar.Crear("Sur", ubicacion));
        Assert.Equal("Ubicacion", error.Campo);
    }

    [Fact]
    public void Crear_GeneraIdsDistintos()
    {
        Assert.NotEqual(ParqueSolar.Crear("Sur", "Montevideo").ID,
            ParqueSolar.Crear("Norte", "Salto").ID);
    }

    [Fact]
    public void Crear_GeneraIdentidadYConservaDatos()
    {
        var parque = ParqueSolar.Crear("Parque Sur", "Montevideo");
        Assert.NotEqual(Guid.Empty, parque.ID);
        Assert.Equal("Parque Sur", parque.Nombre.Valor);
        Assert.Equal("Montevideo", parque.Ubicacion);
    }
}
