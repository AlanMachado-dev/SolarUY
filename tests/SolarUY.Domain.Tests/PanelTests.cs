using System.Reflection;
using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class PanelTests
{
    private static readonly DateTime Instante = new(2026, 10, 29, 12, 34, 56, DateTimeKind.Utc);
    private static Nodo AgregarNodo(ParqueSolar parque) => parque.AgregarNodo("Nodo", 0, 0,
        Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")));
    private static Nodo Nodo() => AgregarNodo(ParqueSolar.Crear("Parque", "Sur"));

    [Fact]
    public void Alta_GeneraIdentidadEstadoPertenenciaYFechaTruncadaSinHistorial()
    {
        var nodo = Nodo();
        var panel = nodo.AgregarPanel("  Panel Á  ", "  Modelo libre  ", 550.5f, Instante.AddTicks(9_999_999));
        var otro = nodo.AgregarPanel("Otro", "Modelo", float.MaxValue, Instante);
        Assert.NotEqual(Guid.Empty, panel.ID);
        Assert.NotEqual(panel.ID, otro.ID);
        Assert.Equal("Panel Á", panel.Nombre.Valor);
        Assert.Equal("Modelo libre", panel.Modelo);
        Assert.Equal(550.5f, panel.PotenciaNominalW);
        Assert.Equal(EstadoPanel.Operativo, panel.Estado);
        Assert.Equal(Instante, panel.FechaInstalacion);
        Assert.Equal(DateTimeKind.Utc, panel.FechaInstalacion.Kind);
        Assert.Same(nodo, panel.Nodo);
        Assert.Same(panel, nodo.Paneles.First());
        Assert.Null(panel.GeneracionActualW);
        Assert.Null(panel.UltimoMantenimiento);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Alta_RechazaTextosObligatoriosSinIncorporacion(string? texto)
    {
        var nodo = Nodo();
        Assert.Equal("Nombre", Assert.Throws<DatosPanelInvalidosException>(() =>
            nodo.AgregarPanel(texto, "Modelo", 1, Instante)).Campo);
        Assert.Equal("Modelo", Assert.Throws<DatosPanelInvalidosException>(() =>
            nodo.AgregarPanel("Panel", texto, 1, Instante)).Campo);
        Assert.Empty(nodo.Paneles);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Alta_RechazaPotenciasInvalidasSinAlterarPanelesPrevios(float potencia)
    {
        var nodo = Nodo();
        var previo = nodo.AgregarPanel("Previo", "M", float.Epsilon, Instante);
        Assert.Equal("PotenciaNominalW", Assert.Throws<DatosPanelInvalidosException>(() =>
            nodo.AgregarPanel("Nuevo", "M", potencia, Instante)).Campo);
        Assert.Same(previo, Assert.Single(nodo.Paneles));
        Assert.Equal(1, nodo.CantidadPaneles);
    }

    [Fact]
    public void Nombre_NormalizaUnicodeMayusculasYExtremosConservandoAcentosYEspaciosInternos()
    {
        var nombre = new NombrePanel("  paNeL a\u0301  sur \t");
        Assert.Equal("paNeL á  sur", nombre.Valor);
        Assert.Equal("PANEL Á  SUR", nombre.Clave);
        Assert.Equal(nombre.Clave, new NombrePanel("PANEL Á  SUR").Clave);
        Assert.NotEqual(nombre.Clave, new NombrePanel("PANEL A  SUR").Clave);
        Assert.NotEqual(nombre.Clave, new NombrePanel("PANEL Á SUR").Clave);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("😀")]
    [InlineData("𐐨")]
    public void Nombre_LimiteCuentaCaracteresUnicodeEnValorYClave(string caracter)
    {
        var limite = string.Concat(Enumerable.Repeat(caracter, 100));
        Assert.Equal(limite, new NombrePanel(limite).Valor);
        Assert.Equal("Nombre", Assert.Throws<DatosPanelInvalidosException>(() => new NombrePanel(limite + caracter)).Campo);
    }

    [Fact]
    public void Alta_UnicidadPorNodoConColeccionProtegidaYNoIncorporacionParcial()
    {
        var nodo = Nodo();
        var vista = nodo.Paneles;
        Assert.Equal(0, nodo.CantidadPaneles);
        var panel = nodo.AgregarPanel("Panel Á", "M", 500, Instante);
        var error = Assert.Throws<DatosPanelInvalidosException>(() => nodo.AgregarPanel(" panel a\u0301 ", "Otro", 1, Instante));
        Assert.True(error.EsNombreDuplicado);
        Assert.Equal("Nombre", error.Campo);
        Assert.Same(panel, Assert.Single(vista));
        Assert.Equal("M", panel.Modelo);
        Assert.Equal(500, panel.PotenciaNominalW);
        var otroNodo = Nodo();
        otroNodo.AgregarPanel("Panel Á", "M", 10, Instante);
        Assert.Equal(1, nodo.CantidadPaneles);
        Assert.Equal(1, otroNodo.CantidadPaneles);
        nodo.AgregarPanel("Panel A", "M", 20, Instante);
        Assert.Equal(2, vista.Count);
        Assert.Equal(2, nodo.CantidadPaneles);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Panel>)vista).Clear());
    }

    [Fact]
    public void Capacidad_SumaPotenciaNominalDeVariosNodosSinNecesitarMediciones()
    {
        var parque = ParqueSolar.Crear("Parque", "Sur");
        Assert.Equal(0m, parque.CapacidadTotalEstimadaMw);
        var primero = AgregarNodo(parque);
        var segundo = AgregarNodo(parque);
        AgregarNodo(parque);
        Assert.Equal(0m, parque.CapacidadTotalEstimadaMw);
        primero.AgregarPanel("A", "M", 500.5f, Instante);
        primero.AgregarPanel("B", "M", 1000, Instante);
        segundo.AgregarPanel("A", "M", 2000, Instante);
        Assert.All(parque.Nodos.SelectMany(nodo => nodo.Paneles), panel => Assert.Null(panel.GeneracionActualW));
        Assert.Equal(0.0035005m, parque.CapacidadTotalEstimadaMw);
        Assert.Equal(0m, ParqueSolar.Crear("Otro", "Norte").CapacidadTotalEstimadaMw);
    }

    [Fact]
    public void Panel_ProtegeConstruccionMutacionYNodoObligatorio()
    {
        Assert.Empty(typeof(Panel).GetConstructors());
        foreach (var tipo in new[] { typeof(Panel), typeof(NombrePanel) })
            Assert.All(tipo.GetProperties(), propiedad => Assert.False(propiedad.SetMethod?.IsPublic == true));
        var crear = typeof(Panel).GetMethod("Crear", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => crear.Invoke(null, ["P", "M", 1f, Instante, null]));
        Assert.Equal("Nodo", Assert.IsType<DatosPanelInvalidosException>(error.InnerException).Campo);
        Assert.Equal("FechaInstalacion", Assert.Throws<DatosPanelInvalidosException>(() =>
            Nodo().AgregarPanel("P", "M", 1, DateTime.SpecifyKind(Instante, DateTimeKind.Unspecified))).Campo);
    }
}
