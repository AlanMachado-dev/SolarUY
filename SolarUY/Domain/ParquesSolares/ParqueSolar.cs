using SolarUY.Domain.Nodos;

namespace SolarUY.Domain.ParquesSolares;

public sealed class ParqueSolar
{
    private readonly List<Nodo> _nodos = [];

    public Guid ID { get; private set; }
    public NombreParqueSolar Nombre { get; private set; } = null!;
    public string Ubicacion { get; private set; } = string.Empty;
    public IReadOnlyCollection<Nodo> Nodos => _nodos.AsReadOnly();
    public IReadOnlyCollection<Guid> NodosIds => _nodos.Select(nodo => nodo.ID).ToList().AsReadOnly();
    public decimal CapacidadTotalEstimadaMw => _nodos.SelectMany(nodo => nodo.Paneles)
        .Sum(panel => (decimal)((double)panel.PotenciaNominalW / 1_000_000d));

    private ParqueSolar() { }

    public Nodo AgregarNodo(string? nombre, float latitud, float longitud, Inversor? inversor)
    {
        var nodo = Nodo.Crear(nombre, latitud, longitud, this, inversor);
        _nodos.Add(nodo);
        return nodo;
    }

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
