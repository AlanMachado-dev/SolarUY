using SolarUY.Domain.ParquesSolares;
using SolarUY.Application.Nodos;

namespace SolarUY.Application.ParquesSolares.Servicios;

public sealed class ParqueSolarService : IParqueSolarService
{
    private readonly IParqueSolarRepository _parqueRepository;
    private readonly IUsuarioActualService _usuarioActual;

    public ParqueSolarService(IParqueSolarRepository parqueRepository, IUsuarioActualService usuarioActual)
    {
        _parqueRepository = parqueRepository;
        _usuarioActual = usuarioActual;
    }

    public async Task<ResultadoListadoParquesSolaresDto> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioActual.EstaAutenticado)
        {
            return new(CodigoConsultaParqueSolar.AutenticacionRequerida, []);
        }

        var parques = await _parqueRepository.ListarAsync(cancellationToken);
        return new(CodigoConsultaParqueSolar.Exitoso, parques.Select(Mapear).ToArray());
    }

    public async Task<ResultadoDetalleParqueSolarDto> ObtenerPorIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioActual.EstaAutenticado)
        {
            return new(CodigoConsultaParqueSolar.AutenticacionRequerida, null, []);
        }

        var parque = await _parqueRepository.ObtenerPorIdAsync(id, cancellationToken);
        return parque is null
            ? new(CodigoConsultaParqueSolar.NoEncontrado, null, [])
            : new(CodigoConsultaParqueSolar.Exitoso, Mapear(parque),
                parque.Nodos.OrderBy(nodo => nodo.Nombre).ThenBy(nodo => nodo.ID)
                    .Select(nodo => new NodoDto(nodo.ID, parque.ID, nodo.Nombre, nodo.Latitud,
                        nodo.Longitud, nodo.CantidadPaneles,
                        new(nodo.Inversor.ID, nodo.Inversor.Nombre, nodo.Inversor.NumeroSerie,
                            new(nodo.Inversor.Sensor.ID, nodo.Inversor.Sensor.Nombre,
                                nodo.Inversor.Sensor.NumeroSerie, nodo.Inversor.Sensor.Endpoint))))
                    .ToArray());
    }

    public async Task<ResultadoAltaParqueSolarDto> CrearAsync(CrearParqueSolarDto datos,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioActual.EstaAutenticado)
        {
            return Rechazar(CodigoErrorAltaParqueSolar.AutenticacionRequerida, "Iniciá sesión para registrar un parque solar.");
        }

        if (!_usuarioActual.PerteneceAlRol("Administrador"))
        {
            return Rechazar(CodigoErrorAltaParqueSolar.AccesoDenegado, "No tenés permiso para registrar parques solares.");
        }

        ArgumentNullException.ThrowIfNull(datos);
        ParqueSolar parque;
        try
        {
            parque = ParqueSolar.Crear(datos.Nombre, datos.Ubicacion);
        }
        catch (DatosParqueSolarInvalidosException error)
        {
            return Rechazar(CodigoErrorAltaParqueSolar.DatosInvalidos, error.Message, error.Campo);
        }

        if (await _parqueRepository.ExisteNombreAsync(parque.Nombre.Clave, cancellationToken))
        {
            return NombreDuplicado();
        }

        var guardado = await _parqueRepository.GuardarAsync(parque, cancellationToken);
        if (guardado == ResultadoGuardadoParqueSolar.ConflictoNombre)
        {
            return NombreDuplicado();
        }

        return new ResultadoAltaParqueSolarDto(
            new ParqueSolarDto(parque.ID, parque.Nombre.Valor, parque.Ubicacion,
                parque.CapacidadTotalEstimadaMw), []);
    }

    private static ResultadoAltaParqueSolarDto NombreDuplicado() =>
        Rechazar(CodigoErrorAltaParqueSolar.NombreDuplicado,
            "Ya existe un parque solar con ese nombre.", nameof(CrearParqueSolarDto.Nombre));

    private static ResultadoAltaParqueSolarDto Rechazar(CodigoErrorAltaParqueSolar codigo,
        string mensaje, string? campo = null) => new(null, [new(codigo, mensaje, campo)]);

    private static ParqueSolarDto Mapear(ParqueSolar parque) =>
        new(parque.ID, parque.Nombre.Valor, parque.Ubicacion, parque.CapacidadTotalEstimadaMw);
}
