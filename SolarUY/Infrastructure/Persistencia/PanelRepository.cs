using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SolarUY.Application.Paneles;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class PanelRepository : IPanelRepository
{
    private readonly SolarUyDbContext _context;

    public PanelRepository(SolarUyDbContext context) => _context = context;

    public async Task<ResultadoGuardadoPanel> GuardarAsync(Panel panel, CancellationToken cancellationToken = default)
    {
        var deteccionAutomatica = _context.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);
            // Los estados explícitos evitan recorrer y guardar el grafo desconectado del nodo.
            _context.ChangeTracker.AutoDetectChangesEnabled = false;
            _context.Entry(panel.Nodo).State = EntityState.Unchanged;
            _context.Entry(panel).Property("IdNodo").CurrentValue = panel.Nodo.ID;
            _context.Entry(panel).State = EntityState.Added;
            _context.Entry(panel.Nombre).Property("IdNodo").CurrentValue = panel.Nodo.ID;
            _context.Entry(panel.Nombre).State = EntityState.Added;
            await _context.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            return ResultadoGuardadoPanel.Guardado;
        }
        catch (DbUpdateException error) when (EsNombreDuplicado(error))
        {
            return ResultadoGuardadoPanel.ConflictoNombre;
        }
        catch (DbUpdateException error) when (EsNodoEliminado(error))
        {
            return ResultadoGuardadoPanel.NodoNoEncontrado;
        }
        finally
        {
            _context.ChangeTracker.Clear();
            _context.ChangeTracker.AutoDetectChangesEnabled = deteccionAutomatica;
        }
    }

    private static bool EsNombreDuplicado(DbUpdateException error)
    {
        if (error.InnerException is not MySqlException mysql || mysql.Number != 1062)
            return false;
        const string separador = " for key '";
        var inicio = mysql.Message.LastIndexOf(separador, StringComparison.Ordinal);
        if (inicio < 0 || !mysql.Message.EndsWith("'", StringComparison.Ordinal))
            return false;
        var partes = mysql.Message[(inicio + separador.Length)..^1].Split('.');
        return partes.Length == 1 && partes[0] == "UX_Paneles_IdNodo_ClaveNombre"
            || partes.Length == 2 && partes[0].Equals("Paneles", StringComparison.OrdinalIgnoreCase)
                && partes[1] == "UX_Paneles_IdNodo_ClaveNombre";
    }

    private static bool EsNodoEliminado(DbUpdateException error) =>
        error.InnerException is MySqlException mysql && mysql.Number == 1452
        && mysql.Message.Contains("CONSTRAINT `FK_Paneles_Nodos_IdNodo`", StringComparison.Ordinal);
}
