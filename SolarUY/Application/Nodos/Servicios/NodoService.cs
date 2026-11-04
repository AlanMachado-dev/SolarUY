using SolarUY.Application.Monitoreo;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.Paneles;
using SolarUY.Domain.Nodos;

namespace SolarUY.Application.Nodos.Servicios;

public sealed class NodoService : INodoService
{
    private readonly INodoRepository _nodoRepository;
    private readonly IParqueSolarRepository _parqueRepository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly IMedicionRepository _medicionRepository;

    public NodoService(INodoRepository nodoRepository, IParqueSolarRepository parqueRepository,
        IUsuarioActualService usuarioActual, IMedicionRepository medicionRepository)
    {
        _nodoRepository = nodoRepository;
        _parqueRepository = parqueRepository;
        _usuarioActual = usuarioActual;
        _medicionRepository = medicionRepository;
    }

    public async Task<ResultadoDetalleNodoDto> ObtenerPorIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioActual.EstaAutenticado)
            return new(CodigoConsultaNodo.AutenticacionRequerida, null);
        if (!_usuarioActual.PerteneceAlRol("Administrador") && !_usuarioActual.PerteneceAlRol("Operador"))
            return new(CodigoConsultaNodo.AccesoDenegado, null);
        if (id == Guid.Empty)
            return new(CodigoConsultaNodo.NoEncontrado, null);

        var nodo = await _nodoRepository.ObtenerPorIdAsync(id, cancellationToken);
        if (nodo is null)
            return new(CodigoConsultaNodo.NoEncontrado, null);

        var mediciones = await _medicionRepository.ObtenerUltimasAsync(
            nodo.Paneles.Select(panel => panel.ID).ToArray(), cancellationToken);
        return new(CodigoConsultaNodo.Exitoso, Mapear(nodo))
        {
            Paneles = nodo.Paneles.OrderBy(panel => panel.Nombre.Valor).ThenBy(panel => panel.ID)
                .Select(panel => PanelDto.Desde(panel, mediciones.GetValueOrDefault(panel.ID))).ToArray()
        };
    }

    public async Task<ResultadoAltaNodoDto> CrearAsync(Guid parqueId, CrearNodoDto datos,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioActual.EstaAutenticado)
            return Rechazar(CodigoErrorAltaNodo.AutenticacionRequerida, "Iniciá sesión para registrar un nodo.");
        if (!_usuarioActual.PerteneceAlRol("Administrador"))
            return Rechazar(CodigoErrorAltaNodo.AccesoDenegado, "No tenés permiso para registrar nodos.");
        if (parqueId == Guid.Empty)
            return ParqueNoEncontrado();

        var parque = await _parqueRepository.ObtenerPorIdAsync(parqueId, cancellationToken);
        if (parque is null)
            return ParqueNoEncontrado();

        ArgumentNullException.ThrowIfNull(datos);
        if (datos.Latitud is null)
            return Rechazar(CodigoErrorAltaNodo.DatosInvalidos, "La latitud es obligatoria.", nameof(datos.Latitud));
        if (datos.Longitud is null)
            return Rechazar(CodigoErrorAltaNodo.DatosInvalidos, "La longitud es obligatoria.", nameof(datos.Longitud));

        Nodo nodo;
        try
        {
            var sensor = Sensor.Crear(datos.NombreSensor, datos.NumeroSerieSensor, datos.EndpointSensor);
            var inversor = Inversor.Crear(datos.NombreInversor, datos.NumeroSerieInversor, sensor);
            nodo = parque.AgregarNodo(datos.Nombre, datos.Latitud.Value, datos.Longitud.Value, inversor);
        }
        catch (DatosNodoInvalidosException error)
        {
            var campo = error.Campo switch
            {
                "Sensor" => nameof(datos.NombreSensor),
                "Inversor" => nameof(datos.NombreInversor),
                _ => error.Campo
            };
            return Rechazar(CodigoErrorAltaNodo.DatosInvalidos, error.Message, campo);
        }

        var erroresSeries = await ValidarSeriesAsync(nodo.Inversor, cancellationToken);
        if (erroresSeries.Count > 0)
            return new(null, erroresSeries);

        var guardado = await _nodoRepository.GuardarAsync(nodo, cancellationToken);
        return guardado switch
        {
            ResultadoGuardadoNodo.Guardado => new(Mapear(nodo), []),
            ResultadoGuardadoNodo.ParqueNoEncontrado => ParqueNoEncontrado(),
            ResultadoGuardadoNodo.ConflictoNumeroSerieInversor => new(null, [InversorDuplicado()]),
            ResultadoGuardadoNodo.ConflictoNumeroSerieSensor => new(null, [SensorDuplicado()]),
            _ => throw new InvalidOperationException("Resultado de guardado de nodo desconocido.")
        };
    }

    private async Task<IReadOnlyList<ErrorAltaNodoDto>> ValidarSeriesAsync(Inversor inversor,
        CancellationToken cancellationToken)
    {
        var errores = new List<ErrorAltaNodoDto>();
        if (await _nodoRepository.ExisteNumeroSerieInversorAsync(inversor.NumeroSerie, cancellationToken))
            errores.Add(InversorDuplicado());
        if (await _nodoRepository.ExisteNumeroSerieSensorAsync(inversor.Sensor.NumeroSerie, cancellationToken))
            errores.Add(SensorDuplicado());
        return errores;
    }

    private static NodoDto Mapear(Nodo nodo) =>
        new(nodo.ID, nodo.Parque.ID, nodo.Nombre, nodo.Latitud, nodo.Longitud, nodo.CantidadPaneles,
            new(nodo.Inversor.ID, nodo.Inversor.Nombre, nodo.Inversor.NumeroSerie,
                new(nodo.Inversor.Sensor.ID, nodo.Inversor.Sensor.Nombre,
                    nodo.Inversor.Sensor.NumeroSerie, nodo.Inversor.Sensor.Endpoint)));

    private static ResultadoAltaNodoDto ParqueNoEncontrado() =>
        Rechazar(CodigoErrorAltaNodo.ParqueNoEncontrado, "No se encontró el parque solar");

    private static ErrorAltaNodoDto InversorDuplicado() =>
        new(CodigoErrorAltaNodo.NumeroSerieInversorDuplicado,
            "Ya existe un inversor con ese número de serie.", nameof(CrearNodoDto.NumeroSerieInversor));

    private static ErrorAltaNodoDto SensorDuplicado() =>
        new(CodigoErrorAltaNodo.NumeroSerieSensorDuplicado,
            "Ya existe un sensor con ese número de serie.", nameof(CrearNodoDto.NumeroSerieSensor));

    private static ResultadoAltaNodoDto Rechazar(CodigoErrorAltaNodo codigo, string mensaje,
        string? campo = null) => new(null, [new(codigo, mensaje, campo)]);
}
