namespace SolarUY.Application.Nodos;

public sealed class CrearNodoDto
{
    public string? Nombre { get; set; }
    public float? Latitud { get; set; }
    public float? Longitud { get; set; }
    public string? NombreInversor { get; set; }
    public string? NumeroSerieInversor { get; set; }
    public string? NombreSensor { get; set; }
    public string? NumeroSerieSensor { get; set; }
    public string? EndpointSensor { get; set; }
}
