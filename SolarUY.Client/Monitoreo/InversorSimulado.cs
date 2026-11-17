namespace SolarUY.Client.Monitoreo;

public sealed class InversorSimulado
{
    public float LeerPotencia(float potenciaNominalW) => potenciaNominalW * Random.Shared.NextSingle();
}
