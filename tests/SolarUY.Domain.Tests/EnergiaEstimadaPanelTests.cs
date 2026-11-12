using SolarUY.Domain.Paneles;
using Xunit;

namespace SolarUY.Domain.Tests;

public class EnergiaEstimadaPanelTests
{
    private static readonly DateTimeOffset _inicio = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculaEnergiaConLaPotenciaAlInicioDelTramo()
    {
        var resultado = Calcular(20, Lectura(0, 360), Lectura(20, 720));

        Assert.Equal(0.002m, resultado.EnergiaKwh);
        Assert.False(resultado.EsParcial);
    }

    [Fact]
    public void DistingueCeroMedidoDeLaFaltaDeDatos()
    {
        Assert.Equal(0m, Calcular(20, Lectura(0, 0), Lectura(20, 720)).EnergiaKwh);
        Assert.Null(Calcular(20).EnergiaKwh);
        Assert.Null(Calcular(20, Lectura(0, 360)).EnergiaKwh);
        Assert.Null(Calcular(20, Lectura(0, 360), Lectura(21, 720)).EnergiaKwh);
    }

    [Fact]
    public void ConservaElMayorIdCuandoCoincideLaFecha()
    {
        var mayor = Lectura(0, 360, new Guid("00000000-0000-0000-0000-000000000002"));
        var menor = Lectura(0, 720, new Guid("00000000-0000-0000-0000-000000000001"));

        Assert.Equal(0.002m, Calcular(20, Lectura(20, 90), mayor, menor).EnergiaKwh);
        Assert.Null(Calcular(20, mayor, menor).EnergiaKwh);
    }

    [Fact]
    public void CuentaSoloLaParteDelTramoPosteriorAMedianoche()
    {
        var resultado = Calcular(10, Lectura(-20, 360), Lectura(10, 720));

        Assert.Equal(0.001m, resultado.EnergiaKwh);
        Assert.False(resultado.EsParcial);
        Assert.Null(Calcular(10, Lectura(-20, 360), Lectura(0, 720)).EnergiaKwh);
    }

    [Fact]
    public void AceptaUnTramoDeCuarentaSegundos()
    {
        var resultado = Calcular(40, Lectura(0, 360), Lectura(40, 360));

        Assert.Equal(0.004m, resultado.EnergiaKwh);
        Assert.False(resultado.EsParcial);
    }

    [Fact]
    public void ConservaLasFraccionesDeSegundo()
    {
        var resultado = EnergiaEstimadaPanel.Calcular(
            [Lectura(0, 360), new(Guid.Empty, _inicio.AddMilliseconds(500), 720)],
            _inicio, _inicio.AddSeconds(1));

        Assert.Equal(0.00005m, resultado.EnergiaKwh);
    }

    [Fact]
    public void OmiteUnHuecoInternoYConservaElSubtotalParcial()
    {
        var resultado = Calcular(81, Lectura(0, 360), Lectura(20, 360),
            Lectura(61, 720), Lectura(81, 720));

        Assert.Equal(0.006m, resultado.EnergiaKwh);
        Assert.True(resultado.EsParcial);
        Assert.Null(Calcular(41, Lectura(0, 360), Lectura(41, 720)).EnergiaKwh);
    }

    [Fact]
    public void IndicaCoberturaParcialSiFaltanDatosAlInicioDelDia()
    {
        var resultado = Calcular(30, Lectura(10, 360), Lectura(30, 360));

        Assert.Equal(0.002m, resultado.EnergiaKwh);
        Assert.True(resultado.EsParcial);
    }

    [Theory]
    [InlineData(60, false)]
    [InlineData(61, true)]
    public void ElFinalPendienteAfectaLaCoberturaSinAgregarEnergia(int corte, bool parcial)
    {
        var resultado = Calcular(corte, Lectura(0, 360), Lectura(20, 720));

        Assert.Equal(0.002m, resultado.EnergiaKwh);
        Assert.Equal(parcial, resultado.EsParcial);
    }

    [Fact]
    public void RechazaUnCorteAnteriorAlInicio()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Calcular(-1));
    }

    [Fact]
    public void RechazaPotenciaNegativaEnUnTramoValido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Calcular(20, Lectura(0, -1), Lectura(20, 0)));
    }

    private static EnergiaEstimadaPanel Calcular(int corte, params LecturaEnergia[] lecturas) =>
        EnergiaEstimadaPanel.Calcular(lecturas, _inicio, _inicio.AddSeconds(corte));

    private static LecturaEnergia Lectura(int segundos, decimal potenciaW, Guid id = default) =>
        new(id, _inicio.AddSeconds(segundos), potenciaW);
}
