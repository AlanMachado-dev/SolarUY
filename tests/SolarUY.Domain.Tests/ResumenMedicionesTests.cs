using SolarUY.Domain.Paneles;
using Xunit;

namespace SolarUY.Domain.Tests;

public class ResumenMedicionesTests
{
    [Fact]
    public void VacioNoEsCeroMedido()
    {
        var vacio = ResumenMediciones.Calcular([]);
        Assert.Equal(0, vacio.Cantidad);
        Assert.Null(vacio.PromedioW);
        Assert.Null(vacio.MinimoW);
        Assert.Null(vacio.MaximoW);
        var cero = ResumenMediciones.Calcular([0]);
        Assert.Equal(1, cero.Cantidad);
        Assert.Equal(0m, cero.PromedioW);
    }

    [Fact]
    public void EstadisticasSinRedondeoPrematuro()
    {
        var resumen = ResumenMediciones.Calcular([0, 1, 3]);
        Assert.Equal(3, resumen.Cantidad);
        Assert.Equal(0, resumen.MinimoW);
        Assert.Equal(3, resumen.MaximoW);
        Assert.Equal(4m / 3, resumen.PromedioW);
        Assert.Throws<ArgumentOutOfRangeException>(() => ResumenMediciones.Calcular([-1]));
    }

    [Fact]
    public void ComposicionPonderaLotesDelMismoPanel()
    {
        var compuesto = ResumenMediciones.Calcular([10]).Componer(ResumenMediciones.Calcular([20, 30, 40]));
        Assert.Equal(25, compuesto.PromedioW);
        Assert.Equal(10, compuesto.MinimoW);
        Assert.Equal(40, compuesto.MaximoW);
        Assert.Equal(compuesto, compuesto.Componer(ResumenMediciones.Vacio));
    }

    [Fact]
    public void AgregacionSumaPromediosPorPanel()
    {
        var potencia = PotenciaObservada.Agregar([ResumenMediciones.Calcular([10]),
            ResumenMediciones.Calcular([20, 40, 60])], 2, true);
        Assert.Equal(50, potencia.PromedioObservadoW);
        Assert.False(potencia.EsParcial);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void AgregacionDeclaraCoberturaParcial(int esperados, bool cobertura)
    {
        var potencia = PotenciaObservada.Agregar([ResumenMediciones.Calcular([0])], esperados, cobertura);
        Assert.True(potencia.EsParcial);
        Assert.Equal(0, potencia.PromedioObservadoW);
        Assert.Equal("NoConcluyente", potencia.CompararCon(potencia).Conclusion);
    }

    [Theory]
    [InlineData(20, 10, 10, 100, "Aumento")]
    [InlineData(5, 10, -5, -50, "Disminucion")]
    [InlineData(10, 10, 0, 0, "Igualdad")]
    public void ComparacionConocida(decimal actual, decimal anterior, decimal diferencia, decimal porcentaje, string conclusion)
    {
        var comparacion = Potencia(actual).CompararCon(Potencia(anterior));
        Assert.Equal(diferencia, comparacion.DiferenciaW);
        Assert.Equal(porcentaje, comparacion.DiferenciaPorcentual);
        Assert.Equal(conclusion, comparacion.Conclusion);
    }

    [Fact]
    public void ReferenciaCeroYAusenciaNoInventanPorcentajes()
    {
        Assert.Null(Potencia(10).CompararCon(Potencia(0)).DiferenciaPorcentual);
        var vacio = PotenciaObservada.Agregar([], 1, false);
        Assert.Null(vacio.PromedioObservadoW);
        Assert.Equal("NoConcluyente", Potencia(10).CompararCon(vacio).Conclusion);
    }

    [Fact]
    public void EstadoActualSeparaVigentesAntiguosYAusentes()
    {
        var ahora = DateTimeOffset.UtcNow;
        var resultado = EstadoActualObservado.Calcular([
            new(ahora, 0, EstadoPanel.EnFalla), new(ahora.AddSeconds(-60), 12, EstadoPanel.Operativo),
            new(ahora.AddSeconds(-61), 100, EstadoPanel.EnFalla), null], ahora, TimeSpan.FromSeconds(60));
        Assert.Equal(12, resultado.PotenciaVigenteW);
        Assert.Equal(2, resultado.PanelesVigentes);
        Assert.Equal(1, resultado.PanelesAntiguos);
        Assert.Equal(1, resultado.PanelesSinMedicion);
        Assert.Equal(2, resultado.Estados.Single(estado => estado.Estado == EstadoPanel.EnFalla).Cantidad);
        Assert.Null(EstadoActualObservado.Calcular([null], ahora, TimeSpan.FromSeconds(60)).PotenciaVigenteW);
        Assert.Throws<ArgumentException>(() => EstadoActualObservado.Calcular([
            new(ahora.AddSeconds(1), 0, EstadoPanel.Operativo)], ahora, TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [InlineData(100, 300, "Disminucion")]
    [InlineData(400, 300, "Aumento")]
    [InlineData(300, 300, "Igualdad")]
    public void CambioObservadoNoConvierteCoberturaParcialEnConclusion(decimal actual, decimal anterior, string cambio)
    {
        var parcial = PotenciaObservada.Agregar([ResumenMediciones.Calcular([actual])], 1, false);
        var comparacion = parcial.CompararCon(Potencia(anterior));
        Assert.Equal(cambio, comparacion.CambioObservado);
        Assert.Equal("NoConcluyente", comparacion.Conclusion);
        Assert.True(comparacion.EsParcial);
    }

    [Fact]
    public void ComparacionSinMuestrasNoInventaCambioObservado()
    {
        var vacio = PotenciaObservada.Agregar([], 1, false);
        Assert.Null(Potencia(10).CompararCon(vacio).CambioObservado);
        Assert.Equal("NoConcluyente", Potencia(10).CompararCon(vacio).Conclusion);
    }

    private static PotenciaObservada Potencia(decimal potencia) =>
        PotenciaObservada.Agregar([ResumenMediciones.Calcular([potencia])], 1, true);
}
