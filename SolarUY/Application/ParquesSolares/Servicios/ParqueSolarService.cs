using SolarUY.Domain.ParquesSolares;

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
}
