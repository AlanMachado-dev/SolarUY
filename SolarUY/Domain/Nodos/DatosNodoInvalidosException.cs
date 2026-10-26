namespace SolarUY.Domain.Nodos;

public sealed class DatosNodoInvalidosException : Exception
{
    public string Campo { get; }

    public DatosNodoInvalidosException(string campo, string mensaje) : base(mensaje)
    {
        Campo = campo;
    }
}
