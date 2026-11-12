namespace SolarUY.Client.Monitoreo;

public sealed class InversorSimulado
{
    private bool _habilitado = true;

    public void Activar() => _habilitado = true;

    public void Desactivar() => _habilitado = false;

    public (float GeneracionActualW, int Estado) Leer(float potenciaNominalW, bool habilitado)
    {
        if (habilitado)
            Activar();
        else
            Desactivar();

        return Leer(potenciaNominalW);
    }

    public (float GeneracionActualW, int Estado) Leer(float potenciaNominalW)
    {
        if (!_habilitado)
            return (0f, 3); // Desactivado.

        // Valores del contrato EstadoPanel recibido por el hub.
        var estado = Random.Shared.Next(20) switch
        {
            0 => 1, // EnFalla
            1 => 2, // EnMantenimiento
            _ => 0  // Operativo
        };
        var potencia = estado == 0 ? potenciaNominalW * Random.Shared.NextSingle() : 0f;
        return (potencia, estado);
    }
}
