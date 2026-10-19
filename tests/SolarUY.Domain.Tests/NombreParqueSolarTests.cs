using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class NombreParqueSolarTests
{
    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    public void Construir_AceptaHastaCienCaracteresSinTruncar(int longitud)
    {
        var texto = new string('a', longitud);
        var nombre = new NombreParqueSolar(texto);
        Assert.Equal(texto, nombre.Valor);
        Assert.Equal(texto.ToUpperInvariant(), nombre.Clave);
    }

    [Fact]
    public void Construir_RechazaCientoUnCaracteresConErrorDeNombre()
    {
        var error = Assert.Throws<DatosParqueSolarInvalidosException>(
            () => new NombreParqueSolar(new string('a', 101)));
        Assert.Equal("Nombre", error.Campo);
        Assert.Equal("El nombre del parque solar no puede superar los 100 caracteres.", error.Message);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    public void Construir_CuentaCaracteresSuplementariosComoUnEscalar(int longitud)
    {
        var texto = string.Concat(Enumerable.Repeat("\U0001F31E", longitud));
        if (longitud == 100)
        {
            var nombre = new NombreParqueSolar(texto);
            Assert.Equal(texto, nombre.Valor);
            Assert.Equal(texto, nombre.Clave);
        }
        else
        {
            Assert.Throws<DatosParqueSolarInvalidosException>(() => new NombreParqueSolar(texto));
        }
    }

    [Fact]
    public void Construir_MideDespuesDeTrimYNormalizacionCanonica()
    {
        var texto = string.Concat(Enumerable.Repeat("e\u0301", 100));
        var nombre = new NombreParqueSolar("  " + texto + "  ");
        Assert.Equal(new string('é', 100), nombre.Valor);
        Assert.Equal(new string('É', 100), nombre.Clave);
    }

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
