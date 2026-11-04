using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class MedicionTests
{
    private static readonly DateTime FechaUtc = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static Panel CrearPanel() => ParqueSolar.Crear("Parque", "Sur")
        .AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")))
        .AgregarPanel("Panel", "Modelo", 500, FechaUtc);

    [Theory]
    [InlineData(0f)]
    [InlineData(125f)]
    public void Crear_ConservaLosDatosDeLaLectura(float potencia)
    {
        var panel = CrearPanel();
        var medicion = Medicion.Crear(panel, FechaUtc, potencia, EstadoPanel.Operativo);

        Assert.NotEqual(Guid.Empty, medicion.ID);
        Assert.Same(panel, medicion.Panel);
        Assert.Equal(FechaUtc, medicion.FechaUtc);
        Assert.Equal(DateTimeKind.Utc, medicion.FechaUtc.Kind);
        Assert.Equal(potencia, medicion.GeneracionActualW);
        Assert.Equal(EstadoPanel.Operativo, medicion.Estado);
    }

    [Fact]
    public void Crear_RechazaPanelAusente() =>
        Assert.Throws<ArgumentNullException>(() => Medicion.Crear(null!, FechaUtc, 0, EstadoPanel.Operativo));

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Crear_RechazaFechaFueraDeUtc(DateTimeKind kind) =>
        Assert.Throws<ArgumentException>(() =>
            Medicion.Crear(CrearPanel(), DateTime.SpecifyKind(FechaUtc, kind), 0, EstadoPanel.Operativo));

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Crear_RechazaPotenciaInvalida(float potencia) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Medicion.Crear(CrearPanel(), FechaUtc, potencia, EstadoPanel.Operativo));

    [Fact]
    public void Crear_RechazaEstadoFueraDelEnumerado() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Medicion.Crear(CrearPanel(), FechaUtc, 0, (EstadoPanel)99));
}
