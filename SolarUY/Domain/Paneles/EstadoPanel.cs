namespace SolarUY.Domain.Paneles;

public enum EstadoPanel
{
    Operativo = 0,
    EnFalla = 1,
    EnMantenimiento = 2,
    Desactivado = 3
}

public enum ResultadoCambioEstadoPanel
{
    Actualizado,
    SinCambios,
    NoPermitido
}
