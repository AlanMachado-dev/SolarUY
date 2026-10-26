namespace SolarUY.Domain.Nodos;

public sealed class Inversor
{
    public Guid ID { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string NumeroSerie { get; private set; } = string.Empty;
    public Sensor Sensor { get; private set; } = null!;
    public Nodo? Nodo { get; private set; }

    private Inversor() { }

    public static Inversor Crear(string? nombre, string? numeroSerie, Sensor? sensor)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosNodoInvalidosException("NombreInversor", "El nombre del inversor es obligatorio.");
        if (string.IsNullOrWhiteSpace(numeroSerie))
            throw new DatosNodoInvalidosException("NumeroSerieInversor", "El número de serie del inversor es obligatorio.");
        if (numeroSerie.Trim().Length > 255)
            throw new DatosNodoInvalidosException("NumeroSerieInversor", "El número de serie del inversor no puede superar los 255 caracteres.");
        if (sensor is null)
            throw new DatosNodoInvalidosException("Sensor", "El sensor es obligatorio.");

        var inversor = new Inversor
        {
            ID = Guid.NewGuid(), Nombre = nombre.Trim(), NumeroSerie = numeroSerie.Trim(), Sensor = sensor
        };
        sensor.AsociarInversor(inversor);
        return inversor;
    }

    internal void AsociarNodo(Nodo nodo)
    {
        if (Nodo is not null)
            throw new DatosNodoInvalidosException("Inversor", "El inversor ya pertenece a un nodo.");
        Nodo = nodo;
    }
}
