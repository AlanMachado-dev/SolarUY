namespace SolarUY.Application.ParquesSolares;

public sealed record ParqueSolarDto(Guid ID, string Nombre, string Ubicacion,
    decimal CapacidadTotalEstimadaMw);
