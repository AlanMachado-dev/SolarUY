namespace SolarUY.Application.Nodos;

public sealed record SensorDto(Guid ID, string Nombre, string NumeroSerie, string? Endpoint);

public sealed record InversorDto(Guid ID, string Nombre, string NumeroSerie, SensorDto Sensor);

public sealed record NodoDto(Guid ID, Guid ParqueId, string Nombre, float Latitud, float Longitud,
    int CantidadPaneles, InversorDto Inversor);
