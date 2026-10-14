using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class NombreParqueSolarTests
{
    [Theory]
    [InlineData("Parque José", "Parque Jose")]
    [InlineData("Parque Sur", "Parque  Sur")]
    public void Construir_ClaveConservaAcentosYEspaciosInteriores(string nombre, string distinto)
    {
        Assert.NotEqual(new NombreParqueSolar(nombre).Clave, new NombreParqueSolar(distinto).Clave);
    }

    [Fact]
    public void Construir_ClaveNormalizaUnicodeCanonico()
    {
        Assert.Equal(new NombreParqueSolar("Parque José").Clave,
            new NombreParqueSolar("Parque Jose\u0301").Clave);
    }

    [Fact]
    public void Construir_ClaveIgnoraMayusculasYEspaciosExteriores()
    {
        Assert.Equal(new NombreParqueSolar("Parque Sur").Clave,
            new NombreParqueSolar(" parque sur ").Clave);
    }

    [Fact]
    public void Construir_ConservaTextoSinEspaciosExteriores()
    {
        Assert.Equal("Parque Sur", new NombreParqueSolar("  Parque Sur  ").Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construir_RechazaNombreObligatorio(string? nombre)
    {
        var error = Assert.Throws<DatosParqueSolarInvalidosException>(
            () => new NombreParqueSolar(nombre));
        Assert.Equal("Nombre", error.Campo);
    }
}
