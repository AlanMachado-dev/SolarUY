using SolarUY.Domain.ParquesSolares;
using SolarUY.Domain.Paneles;

namespace SolarUY.Domain.Nodos;

public sealed class Nodo
{
    private readonly List<Panel> _paneles = [];
    public Guid ID { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public float Latitud { get; private set; }
    public float Longitud { get; private set; }
    public ParqueSolar Parque { get; private set; } = null!;
    public Inversor Inversor { get; private set; } = null!;
    public IReadOnlyCollection<Panel> Paneles => _paneles.AsReadOnly();
    public int CantidadPaneles => _paneles.Count;

    private Nodo() { }

    public Panel AgregarPanel(string? nombre, string? modelo, float potenciaNominalW, DateTime instanteUtc)
    {
        var panel = Panel.Crear(nombre, modelo, potenciaNominalW, instanteUtc, this);
        if (_paneles.Any(existente => existente.Nombre.Clave == panel.Nombre.Clave))
            throw new DatosPanelInvalidosException("Nombre",
                "Ya existe un panel solar con ese nombre en este nodo.", esNombreDuplicado: true);
        _paneles.Add(panel);
        return panel;
    }

    public static Nodo Crear(string? nombre, float latitud, float longitud,
        ParqueSolar? parque, Inversor? inversor)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosNodoInvalidosException("Nombre", "El nombre del nodo es obligatorio.");
        if (!float.IsFinite(latitud) || latitud < -90 || latitud > 90)
            throw new DatosNodoInvalidosException("Latitud", "La latitud debe estar entre -90 y 90.");
        if (!float.IsFinite(longitud) || longitud < -180 || longitud > 180)
            throw new DatosNodoInvalidosException("Longitud", "La longitud debe estar entre -180 y 180.");
        if (parque is null)
            throw new DatosNodoInvalidosException("Parque", "El parque solar es obligatorio.");
        if (inversor is null)
            throw new DatosNodoInvalidosException("Inversor", "El inversor es obligatorio.");

        var nodo = new Nodo
        {
            ID = Guid.NewGuid(), Nombre = nombre.Trim(), Latitud = latitud, Longitud = longitud,
            Parque = parque, Inversor = inversor
        };
        inversor.AsociarNodo(nodo);
        return nodo;
    }
}
