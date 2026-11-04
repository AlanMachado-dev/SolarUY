namespace SolarUY.Client.Monitoreo;

public sealed class InversorSimulado
{
    public (float GeneracionActualW, int Estado) Leer(float potenciaNominalW)
    {
        // Valores del contrato EstadoPanel recibido por el hub.
        var estado = Random.Shared.Next(20) switch
        {
            0 => 1, // EnFalla
            1 => 2, // EnMantenimiento
            2 => 3, // Desactivado
            _ => 0  // Operativo
        };
        var potencia = estado == 0 ? potenciaNominalW * Random.Shared.NextSingle() : 0f;
        return (potencia, estado);
    }
}
