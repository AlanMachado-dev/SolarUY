using System.Data;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class MedicionRepository(IDbContextFactory<SolarUyDbContext> contextFactory, TimeProvider reloj)
    : IMedicionRepository
{
    public async Task<LecturaPanelDto?> ConsultarAsync(Guid panelId, CancellationToken cancellationToken)
    {
        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaccion = await contexto.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var panel = await contexto.Paneles
            .Include(panel => panel.Mantenimientos.Where(mantenimiento => mantenimiento.FechaRealizada == null))
            .SingleOrDefaultAsync(panel => panel.ID == panelId, cancellationToken);
        if (panel is null)
            return null;

        var ultima = await LeerUltimaAsync(contexto, panelId, cancellationToken);
        var revision = ControlPanelRepository.Revision(contexto, panel).CurrentValue;
        await transaccion.CommitAsync(cancellationToken);
        return LecturaPanelDto.Desde(panel, ultima, revision);
    }

    public async Task<ResultadoAdmisionMedicion> AdmitirAsync(MuestraSensorDto muestra, CancellationToken cancellationToken)
    {
        try
        {
            await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaccion = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var panel = await ControlPanelRepository.BloquearPanelAsync(contexto, muestra.PanelId, cancellationToken);
            if (panel is null)
                return new(ResultadoRegistroMedicion.PanelNoEncontrado);

            var ultima = await LeerUltimaAsync(contexto, muestra.PanelId, cancellationToken);
            var ahoraUtc = reloj.GetUtcNow().UtcDateTime;
            var revision = ControlPanelRepository.Revision(contexto, panel);
            var inicial = revision.CurrentValue;
            ultima = ControlPanelRepository.AplicarVencimiento(contexto, panel, ultima, ahoraUtc);
            var codigo = EvaluarMuestra(panel, revision.CurrentValue, muestra);
            if (codigo == ResultadoRegistroMedicion.Guardada)
            {
                ultima = Medicion.CrearLectura(panel, ControlPanelRepository.OrdenarFecha(ahoraUtc, ultima), muestra.GeneracionActualW!.Value);
                contexto.Add(ultima);
            }

            var cambioConfirmable = codigo == ResultadoRegistroMedicion.Guardada || revision.CurrentValue != inicial;
            if (cambioConfirmable)
                await contexto.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            return new(codigo, cambioConfirmable ? LecturaPanelDto.Desde(panel, ultima, revision.CurrentValue) : null);
        }
        catch (Exception error) when (error is DbUpdateException or MySqlException)
        {
            return new(ResultadoRegistroMedicion.ErrorDePersistencia);
        }
    }

    private static ResultadoRegistroMedicion EvaluarMuestra(Panel panel, long revision, MuestraSensorDto muestra)
    {
        if (!panel.AdmiteTelemetria)
            return ResultadoRegistroMedicion.EnPausa;
        if (revision != muestra.RevisionControl)
            return ResultadoRegistroMedicion.RevisionDesactualizada;
        return muestra.GeneracionActualW is { } potencia && float.IsFinite(potencia) && potencia >= 0
            ? ResultadoRegistroMedicion.Guardada : ResultadoRegistroMedicion.LecturaInvalida;
    }

    public async Task<IReadOnlyDictionary<Guid, Medicion>> ObtenerUltimasAsync(
        IReadOnlyCollection<Guid> panelIds, CancellationToken cancellationToken)
    {
        if (panelIds.Count == 0)
            return new Dictionary<Guid, Medicion>();

        await using var contexto = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mediciones = contexto.Set<Medicion>().AsNoTracking();
        var ordenadas = OrdenarPorRecencia(mediciones);
        return await mediciones.Include(medicion => medicion.Panel)
            .Where(medicion => panelIds.Contains(EF.Property<Guid>(medicion, "IdPanel")))
            .Where(medicion => medicion.ID == ordenadas
                .Where(candidata => EF.Property<Guid>(candidata, "IdPanel") == EF.Property<Guid>(medicion, "IdPanel"))
                .Select(candidata => candidata.ID).FirstOrDefault())
            .ToDictionaryAsync(medicion => medicion.Panel.ID, cancellationToken);
    }

    internal static Task<Medicion?> LeerUltimaAsync(SolarUyDbContext contexto, Guid panelId, CancellationToken cancellationToken) =>
        OrdenarPorRecencia(contexto.Set<Medicion>().Where(medicion => EF.Property<Guid>(medicion, "IdPanel") == panelId))
            .FirstOrDefaultAsync(cancellationToken);

    private static IOrderedQueryable<Medicion> OrdenarPorRecencia(IQueryable<Medicion> mediciones) =>
        mediciones.OrderByDescending(medicion => medicion.FechaUtc).ThenByDescending(medicion => medicion.ID);

}
