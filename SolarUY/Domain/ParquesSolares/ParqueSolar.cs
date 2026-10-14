namespace SolarUY.Domain.ParquesSolares;

public sealed class ParqueSolar
{
    public Guid ID { get; private set; }
    public NombreParqueSolar Nombre { get; private set; } = null!;
    public string Ubicacion { get; private set; } = string.Empty;
    // La suma de una topología vacía es cero; la topología se incorpora en otro requerimiento.
    public decimal CapacidadTotalEstimadaMw => 0m;

    private ParqueSolar() { }

    public static ParqueSolar Crear(string? nombre, string? ubicacion)
    {
        var nombreParque = new NombreParqueSolar(nombre);
        if (string.IsNullOrWhiteSpace(ubicacion))
        {
            throw new DatosParqueSolarInvalidosException("Ubicacion", "La ubicación es obligatoria.");
        }

        return new ParqueSolar
        {
            ID = Guid.NewGuid(),
            Nombre = nombreParque,
            Ubicacion = ubicacion.Trim()
        };
    }
}
