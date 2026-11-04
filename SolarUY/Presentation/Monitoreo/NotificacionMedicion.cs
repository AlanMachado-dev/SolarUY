using System.Security.Claims;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.Monitoreo;

public sealed class NotificacionMedicion : INotificacionMedicion
{
    public const string RolesConsulta = "Administrador,Operador";

    private readonly object _bloqueo = new();
    private readonly Dictionary<Guid, List<Suscripcion>> _suscripciones = [];

    public IDisposable Suscribir(ClaimsPrincipal usuario, Guid panelId, Func<LecturaPanelDto, Task> recibir)
    {
        if (usuario.Identity?.IsAuthenticated != true ||
            !(usuario.IsInRole("Administrador") || usuario.IsInRole("Operador")))
            throw new UnauthorizedAccessException();

        var suscripcion = new Suscripcion(this, panelId, recibir);
        lock (_bloqueo)
        {
            if (!_suscripciones.TryGetValue(panelId, out var lista))
                _suscripciones.Add(panelId, lista = []);
            lista.Add(suscripcion);
        }
        return suscripcion;
    }

    public Task PublicarAsync(LecturaPanelDto lectura)
    {
        Suscripcion[] destinatarios;
        lock (_bloqueo)
            destinatarios = _suscripciones.TryGetValue(lectura.PanelId, out var lista) ? [.. lista] : [];

        return Task.WhenAll(destinatarios.Select(suscripcion => suscripcion.EntregarAsync(lectura)));
    }

    private void Retirar(Suscripcion suscripcion)
    {
        lock (_bloqueo)
        {
            if (_suscripciones.TryGetValue(suscripcion.PanelId, out var lista) &&
                lista.Remove(suscripcion) && lista.Count == 0)
                _suscripciones.Remove(suscripcion.PanelId);
        }
    }

    private sealed class Suscripcion(NotificacionMedicion origen, Guid panelId, Func<LecturaPanelDto, Task> recibir)
        : IDisposable
    {
        public Guid PanelId { get; } = panelId;

        public async Task EntregarAsync(LecturaPanelDto lectura)
        {
            try { await recibir(lectura); }
            catch (InvalidOperationException) { } // El circuito pudo cerrarse durante la entrega.
            catch (OperationCanceledException) { }
        }

        public void Dispose() => origen.Retirar(this);
    }
}
