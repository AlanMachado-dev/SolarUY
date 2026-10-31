using SolarUY.Application.Nodos;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Paneles.Servicios;

public sealed class PanelService : IPanelService
{
    private readonly INodoRepository _nodoRepository;
    private readonly IPanelRepository _panelRepository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly TimeProvider _reloj;

    public PanelService(INodoRepository nodoRepository, IPanelRepository panelRepository,
        IUsuarioActualService usuarioActual, TimeProvider reloj)
    {
        _nodoRepository = nodoRepository;
        _panelRepository = panelRepository;
        _usuarioActual = usuarioActual;
        _reloj = reloj;
    }

    public async Task<ResultadoAltaPanelDto> CrearAsync(Guid nodoId, CrearPanelDto datos,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioActual.EstaAutenticado)
            return Rechazar(CodigoErrorAltaPanel.AutenticacionRequerida, "Iniciá sesión para registrar un panel solar.");
        if (!_usuarioActual.PerteneceAlRol("Administrador"))
            return Rechazar(CodigoErrorAltaPanel.AccesoDenegado, "No tenés permiso para registrar paneles solares.");
        if (nodoId == Guid.Empty)
            return NodoNoEncontrado();

        var nodo = await _nodoRepository.ObtenerPorIdAsync(nodoId, cancellationToken);
        if (nodo is null)
            return NodoNoEncontrado();
        ArgumentNullException.ThrowIfNull(datos);
        if (datos.PotenciaNominalW is null)
            return Rechazar(CodigoErrorAltaPanel.DatosInvalidos, "La potencia nominal es obligatoria.", nameof(datos.PotenciaNominalW));

        Panel panel;
        try
        {
            panel = nodo.AgregarPanel(datos.Nombre, datos.Modelo, datos.PotenciaNominalW.Value, _reloj.GetUtcNow().UtcDateTime);
        }
        catch (DatosPanelInvalidosException error)
        {
            return Rechazar(error.EsNombreDuplicado ? CodigoErrorAltaPanel.NombreDuplicado : CodigoErrorAltaPanel.DatosInvalidos,
                error.Message, error.Campo);
        }

        var guardado = await _panelRepository.GuardarAsync(panel, cancellationToken);
        return guardado switch
        {
            ResultadoGuardadoPanel.Guardado => new(PanelDto.Desde(panel), []),
            ResultadoGuardadoPanel.ConflictoNombre => Rechazar(CodigoErrorAltaPanel.NombreDuplicado,
                "Ya existe un panel solar con ese nombre en este nodo.", nameof(datos.Nombre)),
            ResultadoGuardadoPanel.NodoNoEncontrado => NodoNoEncontrado(),
            _ => throw new InvalidOperationException("Resultado de guardado de panel desconocido.")
        };
    }

    private static ResultadoAltaPanelDto NodoNoEncontrado() =>
        Rechazar(CodigoErrorAltaPanel.NodoNoEncontrado, "No se encontró el nodo");

    private static ResultadoAltaPanelDto Rechazar(CodigoErrorAltaPanel codigo, string mensaje,
        string? campo = null) => new(null, [new(codigo, mensaje, campo)]);
}
