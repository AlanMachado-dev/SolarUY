namespace SolarUY.Domain.ParquesSolares;

public sealed class DatosParqueSolarInvalidosException : Exception
{
    public string Campo { get; }

    public DatosParqueSolarInvalidosException(string campo, string mensaje) : base(mensaje)
    {
        Campo = campo;
    }
}
