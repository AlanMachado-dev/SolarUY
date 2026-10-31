namespace SolarUY.Domain.Paneles;

public sealed class DatosPanelInvalidosException : Exception
{
    public string Campo { get; }
    public bool EsNombreDuplicado { get; }

    public DatosPanelInvalidosException(string campo, string mensaje, bool esNombreDuplicado = false)
        : base(mensaje)
    {
        Campo = campo;
        EsNombreDuplicado = esNombreDuplicado;
    }
}
