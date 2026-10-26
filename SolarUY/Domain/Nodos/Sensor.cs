namespace SolarUY.Domain.Nodos;

public sealed class Sensor
{
    public Guid ID { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string NumeroSerie { get; private set; } = string.Empty;
    public string? Endpoint { get; private set; }
    public Inversor? Inversor { get; private set; }

    private Sensor() { }

    public static Sensor Crear(string? nombre, string? numeroSerie, string? endpoint = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosNodoInvalidosException("NombreSensor", "El nombre del sensor es obligatorio.");
        if (string.IsNullOrWhiteSpace(numeroSerie))
            throw new DatosNodoInvalidosException("NumeroSerieSensor", "El número de serie del sensor es obligatorio.");
        if (numeroSerie.Trim().Length > 255)
            throw new DatosNodoInvalidosException("NumeroSerieSensor", "El número de serie del sensor no puede superar los 255 caracteres.");

        return new Sensor
        {
            ID = Guid.NewGuid(), Nombre = nombre.Trim(), NumeroSerie = numeroSerie.Trim(),
            Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim()
        };
    }

    internal void AsociarInversor(Inversor inversor)
    {
        if (Inversor is not null)
            throw new DatosNodoInvalidosException("Sensor", "El sensor ya pertenece a un inversor.");
        Inversor = inversor;
    }
}
