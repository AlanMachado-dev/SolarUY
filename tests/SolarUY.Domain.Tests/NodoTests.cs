using SolarUY.Domain.Nodos;
using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class NodoTests
{
    private static ParqueSolar Parque() => ParqueSolar.Crear("Sur", "Montevideo");
    private static Inversor Equipo() => Inversor.Crear("Inversor", "I-1", Sensor.Crear("Sensor", "S-1"));

    [Fact]
    public void Inversor_Acepta255CaracteresRecortadosYRechaza256()
    {
        var serie = new string('I', 255);
        Assert.Equal(serie, Inversor.Crear("i", " " + serie + " ", Sensor.Crear("s", "s")).NumeroSerie);
        Assert.Equal("NumeroSerieInversor", Assert.Throws<DatosNodoInvalidosException>(() =>
            Inversor.Crear("i", serie + "I", Sensor.Crear("s", "s"))).Campo);
    }

    [Fact]
    public void Sensor_Acepta255CaracteresRecortadosYRechaza256()
    {
        var serie = new string('S', 255);
        Assert.Equal(serie, Sensor.Crear("s", " " + serie + " ").NumeroSerie);
        Assert.Equal("NumeroSerieSensor", Assert.Throws<DatosNodoInvalidosException>(() =>
            Sensor.Crear("s", serie + "S")).Campo);
    }

    [Theory]
    [InlineData("  INV  Áé-01 \t", "INV  Áé-01")]
    [InlineData("\n inv áÉ-01  ", "inv áÉ-01")]
    public void Series_RecortanSoloEspaciosExternosYConservanMayusculasYAcentos(string entrada, string esperado)
    {
        var sensor = Sensor.Crear("s", entrada);
        Assert.Equal(esperado, sensor.NumeroSerie);
        Assert.Equal(esperado, Inversor.Crear("i", entrada, sensor).NumeroSerie);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_RechazaTextosObligatorios(string? texto)
    {
        Assert.Equal("NombreSensor", Assert.Throws<DatosNodoInvalidosException>(() => Sensor.Crear(texto, "s")).Campo);
        Assert.Equal("NumeroSerieSensor", Assert.Throws<DatosNodoInvalidosException>(() => Sensor.Crear("s", texto)).Campo);
        Assert.Equal("NombreInversor", Assert.Throws<DatosNodoInvalidosException>(() => Inversor.Crear(texto, "i", Sensor.Crear("s", "s"))).Campo);
        Assert.Equal("NumeroSerieInversor", Assert.Throws<DatosNodoInvalidosException>(() => Inversor.Crear("i", texto, Sensor.Crear("s", "s"))).Campo);
        Assert.Equal("Nombre", Assert.Throws<DatosNodoInvalidosException>(() => Parque().AgregarNodo(texto, 0, 0, Equipo())).Campo);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  sin protocolo  ", "sin protocolo")]
    public void Sensor_NormalizaEndpointSinExigirProtocolo(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, Sensor.Crear("sensor", "serie", entrada).Endpoint);
    }

    [Fact]
    public void Crear_RecortaTextosYGeneraIdentidadesParaTodoElGrafo()
    {
        var sensor = Sensor.Crear(" sensor ", " serie sensor ");
        var inversor = Inversor.Crear(" inversor ", " serie inversor ", sensor);
        var parque = Parque();
        var nodo = parque.AgregarNodo(" nodo ", -34.9f, -56.2f, inversor);
        var otro = parque.AgregarNodo("nodo", 0, 0, Equipo());
        var ids = new[] { nodo.ID, inversor.ID, sensor.ID, otro.ID, otro.Inversor.ID, otro.Inversor.Sensor.ID };
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal("sensor", sensor.Nombre);
        Assert.Equal("serie sensor", sensor.NumeroSerie);
        Assert.Equal("inversor", inversor.Nombre);
        Assert.Equal("serie inversor", inversor.NumeroSerie);
        Assert.Equal("nodo", nodo.Nombre);
        Assert.Equal(-34.9f, nodo.Latitud);
        Assert.Equal(-56.2f, nodo.Longitud);
        Assert.Same(parque, nodo.Parque);
        Assert.Same(inversor, nodo.Inversor);
        Assert.Same(sensor, inversor.Sensor);
        Assert.Same(nodo, inversor.Nodo);
        Assert.Same(inversor, sensor.Inversor);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-90f, -180f)]
    [InlineData(90f, 180f)]
    public void AgregarNodo_AceptaCeroYLimites(float latitud, float longitud)
    {
        var nodo = Parque().AgregarNodo("n", latitud, longitud, Equipo());
        Assert.Equal(latitud, nodo.Latitud);
        Assert.Equal(longitud, nodo.Longitud);
    }

    [Theory]
    [InlineData(-90.01f)]
    [InlineData(90.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void AgregarNodo_RechazaLatitudInvalidaSinIncorporacionParcial(float latitud)
    {
        var parque = Parque();
        var inversor = Equipo();
        Assert.Equal("Latitud", Assert.Throws<DatosNodoInvalidosException>(() => parque.AgregarNodo("n", latitud, 0, inversor)).Campo);
        Assert.Empty(parque.Nodos);
        Assert.Empty(parque.NodosIds);
        Assert.Null(inversor.Nodo);
    }

    [Theory]
    [InlineData(-180.01f)]
    [InlineData(180.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void AgregarNodo_RechazaLongitudInvalida(float longitud)
    {
        var parque = Parque();
        Assert.Equal("Longitud", Assert.Throws<DatosNodoInvalidosException>(() => parque.AgregarNodo("n", 0, longitud, Equipo())).Campo);
        Assert.Empty(parque.Nodos);
    }

    [Fact]
    public void Crear_RechazaRelacionesAusentes()
    {
        Assert.Equal("Sensor", Assert.Throws<DatosNodoInvalidosException>(() => Inversor.Crear("i", "i", null)).Campo);
        Assert.Equal("Parque", Assert.Throws<DatosNodoInvalidosException>(() => Nodo.Crear("n", 0, 0, null, Equipo())).Campo);
        var parque = Parque();
        Assert.Equal("Inversor", Assert.Throws<DatosNodoInvalidosException>(() => parque.AgregarNodo("n", 0, 0, null)).Campo);
        Assert.Empty(parque.Nodos);
    }

    [Fact]
    public void AgregarNodo_PreservaPertenenciaYNoComparteEquipos()
    {
        var parque = Parque();
        var otroParque = Parque();
        var inversor = Equipo();
        var nodo = parque.AgregarNodo("n", 0, 0, inversor);
        Assert.Throws<DatosNodoInvalidosException>(() => parque.AgregarNodo("otro", 0, 0, inversor));
        Assert.Throws<DatosNodoInvalidosException>(() => otroParque.AgregarNodo("otro", 0, 0, inversor));
        Assert.Throws<DatosNodoInvalidosException>(() => Inversor.Crear("otro", "otro", inversor.Sensor));
        Assert.Same(nodo, inversor.Nodo);
        Assert.Same(inversor, inversor.Sensor.Inversor);
        Assert.Same(parque, nodo.Parque);
        Assert.Single(parque.Nodos);
        Assert.Empty(otroParque.Nodos);
    }

    [Fact]
    public void Colecciones_ProtegidasConIdsProyectadosYCapacidadSinPaneles()
    {
        var parque = Parque();
        Assert.Empty(parque.Nodos);
        Assert.Empty(parque.NodosIds);
        var vista = parque.Nodos;
        var primero = parque.AgregarNodo("n", 0, 0, Equipo());
        var segundo = parque.AgregarNodo("n", 0, 0, Equipo());
        Assert.Equal(new[] { primero.ID, segundo.ID }, parque.NodosIds);
        Assert.Equal(2, vista.Count);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Nodo>)parque.Nodos).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<Guid>)parque.NodosIds).Clear());
        Assert.All(parque.Nodos, nodo => Assert.Equal(0, nodo.CantidadPaneles));
        Assert.Equal(0m, parque.CapacidadTotalEstimadaMw);
    }

    [Fact]
    public void Entidades_NoExponenSettersPublicosNiConstructoresPublicos()
    {
        foreach (var tipo in new[] { typeof(Nodo), typeof(Inversor), typeof(Sensor) })
        {
            Assert.Empty(tipo.GetConstructors());
            Assert.All(tipo.GetProperties(), propiedad => Assert.False(propiedad.SetMethod?.IsPublic == true));
        }
    }
}
