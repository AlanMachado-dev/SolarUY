using SolarUY.Domain.Nodos;
using SolarUY.Domain.Paneles;
using SolarUY.Domain.ParquesSolares;
using Xunit;

namespace SolarUY.Domain.Tests;

public class MantenimientoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Programar_GeneraIdentidadYVinculaElRegistroAlPanel()
    {
        var panel = CrearPanel();
        var fecha = Ahora.AddHours(1);
        var mantenimiento = panel.ProgramarMantenimiento(" Limpieza ", fecha, " Técnico ", Ahora);

        Assert.NotEqual(Guid.Empty, mantenimiento.ID);
        Assert.NotEqual(mantenimiento.ID, Programar(CrearPanel(), fecha).ID);
        Assert.Same(panel, mantenimiento.Panel);
        Assert.Equal("Limpieza", mantenimiento.Tipo);
        Assert.Equal("Técnico", mantenimiento.Responsable);
        Assert.Equal(fecha, mantenimiento.FechaProgramada);
        Assert.Null(mantenimiento.InicioProcesadoUtc);
        Assert.Null(mantenimiento.FechaRealizada);
        Assert.Null(mantenimiento.Observaciones);
    }

    [Theory]
    [InlineData(null, "Técnico", nameof(Mantenimiento.Tipo))]
    [InlineData(" ", "Técnico", nameof(Mantenimiento.Tipo))]
    [InlineData("Limpieza", null, nameof(Mantenimiento.Responsable))]
    [InlineData("Limpieza", " ", nameof(Mantenimiento.Responsable))]
    public void Programar_RechazaDatosObligatorios(string? tipo, string? responsable, string campo)
    {
        var panel = CrearPanel();
        var error = Assert.Throws<DatosPanelInvalidosException>(() =>
            panel.ProgramarMantenimiento(tipo, Ahora, responsable, Ahora));
        Assert.Equal(campo, error.Campo);
        Assert.Empty(panel.Mantenimientos);
    }

    [Fact]
    public void Programar_RechazaFechaAusenteOQueNoEstaEnUtc()
    {
        var panel = CrearPanel();
        foreach (var fecha in new[] { default, DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified) })
            Assert.Equal(nameof(Mantenimiento.FechaProgramada), Assert.Throws<DatosPanelInvalidosException>(
                () => Programar(panel, fecha)).Campo);
        Assert.Empty(panel.Mantenimientos);
    }

    [Fact]
    public void ProgramarAFuturo_ConservaEstadoYRechazaDuplicado()
    {
        var panel = CrearPanel();
        var mantenimiento = Programar(panel, Ahora.AddHours(1));
        Assert.Equal(EstadoPanel.Operativo, panel.Estado);
        Assert.Same(mantenimiento, panel.MantenimientoAbierto);
        Assert.Throws<DatosPanelInvalidosException>(() => Programar(panel, Ahora));
        Assert.Same(mantenimiento, Assert.Single(panel.Mantenimientos));
        Assert.Throws<NotSupportedException>(() => ((ICollection<Mantenimiento>)panel.Mantenimientos).Clear());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Vencimiento_IniciaAlLlegarLaFechaUnicamenteUnaVez(int retrasoSegundos)
    {
        var panel = CrearPanel();
        var fecha = Ahora.AddHours(1);
        var mantenimiento = Programar(panel, fecha);
        Assert.False(panel.IniciarMantenimientoVencido(fecha.AddTicks(-1)));
        Assert.Equal(EstadoPanel.Operativo, panel.Estado);

        var inicio = fecha.AddSeconds(retrasoSegundos);
        Assert.True(panel.IniciarMantenimientoVencido(inicio));
        Assert.False(panel.IniciarMantenimientoVencido(inicio.AddSeconds(1)));
        Assert.Equal(inicio, mantenimiento.InicioProcesadoUtc);
        Assert.Equal(EstadoPanel.EnMantenimiento, panel.Estado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Programar_IniciaEnElMomentoDeConfirmacionSiLaFechaYaLlego(int segundos)
    {
        var panel = CrearPanel();
        var mantenimiento = Programar(panel, Ahora.AddSeconds(segundos));
        Assert.True(mantenimiento.EnCurso);
        Assert.Equal(Ahora, mantenimiento.InicioProcesadoUtc);
        Assert.Equal(EstadoPanel.EnMantenimiento, panel.Estado);
    }

    [Theory]
    [InlineData(EstadoPanel.EnFalla)]
    [InlineData(EstadoPanel.Desactivado)]
    public void Vencimiento_ConservaFallaODesactivacion(EstadoPanel estado)
    {
        var panel = CrearPanel();
        Pausar(panel, estado);
        var fecha = Ahora.AddMinutes(1);
        var mantenimiento = Programar(panel, fecha);
        Assert.True(panel.IniciarMantenimientoVencido(fecha));
        Assert.True(mantenimiento.EnCurso);
        Assert.Equal(estado, panel.Estado);
    }

    [Theory]
    [InlineData(EstadoPanel.EnFalla, EstadoPanel.Desactivado)]
    [InlineData(EstadoPanel.Desactivado, EstadoPanel.EnFalla)]
    public void Alternar_ConservaElMantenimientoYPrevaleceLaUltimaOrden(EstadoPanel primera, EstadoPanel segunda)
    {
        var panel = CrearPanel();
        var mantenimiento = Programar(panel, Ahora);
        Assert.Equal(ResultadoCambioEstadoPanel.Actualizado, Pausar(panel, primera));
        Assert.Equal(ResultadoCambioEstadoPanel.SinCambios, Pausar(panel, primera));
        Assert.Equal(ResultadoCambioEstadoPanel.Actualizado, Pausar(panel, segunda));
        Assert.Equal(ResultadoCambioEstadoPanel.SinCambios, Pausar(panel, segunda));
        Assert.Equal(ResultadoCambioEstadoPanel.NoPermitido, Recuperar(panel, primera));
        Assert.Equal(segunda, panel.Estado);
        Assert.True(mantenimiento.EnCurso);
    }

    [Theory]
    [InlineData(EstadoPanel.EnFalla, 1, false, EstadoPanel.Operativo)]
    [InlineData(EstadoPanel.Desactivado, 1, false, EstadoPanel.Operativo)]
    [InlineData(EstadoPanel.EnFalla, 0, false, EstadoPanel.EnMantenimiento)]
    [InlineData(EstadoPanel.Desactivado, 0, false, EstadoPanel.EnMantenimiento)]
    [InlineData(EstadoPanel.EnFalla, 0, true, EstadoPanel.Operativo)]
    [InlineData(EstadoPanel.Desactivado, 0, true, EstadoPanel.Operativo)]
    public void Recuperar_RespetaElCicloDeMantenimiento(EstadoPanel estado, int demoraHoras, bool finalizado, EstadoPanel destino)
    {
        var panel = CrearPanel();
        var mantenimiento = Programar(panel, Ahora.AddHours(demoraHoras));
        Pausar(panel, estado);
        if (finalizado)
            panel.FinalizarMantenimiento(mantenimiento.ID, Ahora, null);

        Assert.Equal(ResultadoCambioEstadoPanel.Actualizado, Recuperar(panel, estado));
        Assert.Equal(destino, panel.Estado);
        Assert.Equal(ResultadoCambioEstadoPanel.SinCambios, Recuperar(panel, estado));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData(" Limpieza completa ", "Limpieza completa")]
    public void Finalizar_RegistraElInstanteYObservacionesUnaSolaVez(string? observaciones, string? guardadas)
    {
        var panel = CrearPanel();
        var fecha = Ahora.AddMinutes(1);
        var mantenimiento = Programar(panel, fecha);
        Assert.Equal(ResultadoCambioEstadoPanel.NoPermitido, panel.FinalizarMantenimiento(mantenimiento.ID, Ahora, null));
        Assert.Null(mantenimiento.FechaRealizada);
        panel.IniciarMantenimientoVencido(fecha);
        var cierre = fecha.AddMinutes(1);
        Assert.Equal(ResultadoCambioEstadoPanel.Actualizado, panel.FinalizarMantenimiento(mantenimiento.ID, cierre, observaciones));
        var siguiente = panel.ProgramarMantenimiento("Revisión", cierre, "Técnico", cierre);
        Assert.Equal(ResultadoCambioEstadoPanel.SinCambios, panel.FinalizarMantenimiento(mantenimiento.ID, cierre.AddMinutes(1), "Otro texto"));
        Assert.Equal(cierre, mantenimiento.FechaRealizada);
        Assert.Equal(DateTimeKind.Utc, mantenimiento.FechaRealizada!.Value.Kind);
        Assert.Equal(guardadas, mantenimiento.Observaciones);
        Assert.Same(siguiente, panel.MantenimientoAbierto);
    }

    [Theory]
    [InlineData(EstadoPanel.EnMantenimiento, EstadoPanel.Operativo)]
    [InlineData(EstadoPanel.EnFalla, EstadoPanel.EnFalla)]
    [InlineData(EstadoPanel.Desactivado, EstadoPanel.Desactivado)]
    public void Finalizar_RecuperaSoloElEstadoDeMantenimiento(EstadoPanel estado, EstadoPanel destino)
    {
        var panel = CrearPanel();
        var mantenimiento = Programar(panel, Ahora);
        if (estado != EstadoPanel.EnMantenimiento)
            Pausar(panel, estado);

        Assert.Equal(ResultadoCambioEstadoPanel.Actualizado, panel.FinalizarMantenimiento(mantenimiento.ID, Ahora, null));
        Assert.Equal(destino, panel.Estado);
        Assert.Null(panel.MantenimientoAbierto);
    }

    [Fact]
    public void UltimoMantenimiento_UsaLaFechaRealizadaMasReciente()
    {
        var panel = CrearPanel();
        Assert.Null(panel.UltimoMantenimiento);
        var primero = Programar(panel, Ahora);
        Assert.Null(panel.UltimoMantenimiento);
        var cierre = Ahora.AddHours(2);
        panel.FinalizarMantenimiento(primero.ID, cierre, null);
        var segundo = Programar(panel, Ahora);
        panel.FinalizarMantenimiento(segundo.ID, Ahora.AddHours(1), null);
        Programar(panel, cierre.AddDays(1));
        Assert.Equal(cierre, panel.UltimoMantenimiento);
    }

    [Theory]
    [InlineData(EstadoPanel.EnMantenimiento)]
    [InlineData(EstadoPanel.EnFalla)]
    [InlineData(EstadoPanel.Desactivado)]
    public void Telemetria_RechazaMuestrasDurantePausasYAdmiteRegistrosDeControl(EstadoPanel estado)
    {
        var panel = CrearPanel();
        var lectura = Medicion.CrearLectura(panel, Ahora, 125);
        Assert.Equal(EstadoPanel.Operativo, lectura.Estado);
        Assert.Equal(125, lectura.GeneracionActualW);
        if (estado == EstadoPanel.EnMantenimiento)
            Programar(panel, Ahora);
        else
            Pausar(panel, estado);

        Assert.False(panel.AdmiteTelemetria);
        Assert.Equal(0, panel.GeneracionActualW);
        Assert.Throws<InvalidOperationException>(() => Medicion.CrearLectura(panel, Ahora, 125));
        var control = Medicion.CrearControl(panel, Ahora);
        Assert.Equal(estado, control.Estado);
        Assert.Equal(0, control.GeneracionActualW);
    }

    private static Panel CrearPanel() => ParqueSolar.Crear("Parque", "Sur")
        .AgregarNodo("Nodo", 0, 0, Inversor.Crear("Inversor", "I", Sensor.Crear("Sensor", "S")))
        .AgregarPanel("Panel", "M", 500, Ahora);

    private static Mantenimiento Programar(Panel panel, DateTime fecha) =>
        panel.ProgramarMantenimiento("Limpieza", fecha, "Técnico", Ahora);

    private static ResultadoCambioEstadoPanel Pausar(Panel panel, EstadoPanel estado) =>
        estado == EstadoPanel.EnFalla ? panel.SimularFalla() : panel.Desactivar();

    private static ResultadoCambioEstadoPanel Recuperar(Panel panel, EstadoPanel estado) =>
        estado == EstadoPanel.EnFalla ? panel.SimularArreglo() : panel.Activar();
}
