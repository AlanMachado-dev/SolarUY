using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SolarUY.Application.Nodos;
using SolarUY.Domain.Nodos;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class NodoRepository : INodoRepository
{
    private readonly SolarUyDbContext _context;

    public NodoRepository(SolarUyDbContext context)
    {
        _context = context;
    }

    public Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Nodos.AsNoTracking()
            .Include(nodo => nodo.Paneles)
            .Include(nodo => nodo.Parque)
            .Include(nodo => nodo.Inversor).ThenInclude(inversor => inversor.Sensor)
            .SingleOrDefaultAsync(nodo => nodo.ID == id, cancellationToken);

    public Task<bool> ExisteNumeroSerieInversorAsync(string numeroSerie,
        CancellationToken cancellationToken = default) =>
        _context.Inversores.AnyAsync(inversor => inversor.NumeroSerie == numeroSerie, cancellationToken);

    public Task<bool> ExisteNumeroSerieSensorAsync(string numeroSerie,
        CancellationToken cancellationToken = default) =>
        _context.Sensores.AnyAsync(sensor => sensor.NumeroSerie == numeroSerie, cancellationToken);

    public async Task<ResultadoGuardadoNodo> GuardarAsync(Nodo nodo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);
            // Entry no recorre el grafo: los otros nodos del parque no se agregan ni actualizan.
            _context.Entry(nodo.Parque).State = EntityState.Unchanged;
            _context.Entry(nodo.Parque.Nombre).State = EntityState.Unchanged;
            _context.Entry(nodo).State = EntityState.Added;
            _context.Entry(nodo.Inversor).State = EntityState.Added;
            _context.Entry(nodo.Inversor.Sensor).State = EntityState.Added;
            await _context.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            return ResultadoGuardadoNodo.Guardado;
        }
        catch (DbUpdateException error) when (EsSerieDuplicada(error, "Inversores", "UX_Inversores_NumeroSerie"))
        {
            _context.ChangeTracker.Clear();
            return ResultadoGuardadoNodo.ConflictoNumeroSerieInversor;
        }
        catch (DbUpdateException error) when (EsSerieDuplicada(error, "Sensores", "UX_Sensores_NumeroSerie"))
        {
            _context.ChangeTracker.Clear();
            return ResultadoGuardadoNodo.ConflictoNumeroSerieSensor;
        }
        catch (DbUpdateException error) when (EsParqueEliminado(error))
        {
            _context.ChangeTracker.Clear();
            return ResultadoGuardadoNodo.ParqueNoEncontrado;
        }
        catch
        {
            // El dispose de la transacción revierte incluso si falla SaveChanges o Commit.
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool EsSerieDuplicada(DbUpdateException error, string tabla, string indice)
    {
        if (error.InnerException is not MySqlException mysql || mysql.Number != 1062)
            return false;

        const string separador = " for key '";
        var inicio = mysql.Message.LastIndexOf(separador, StringComparison.Ordinal);
        if (inicio < 0 || !mysql.Message.EndsWith("'", StringComparison.Ordinal))
            return false;

        var clave = mysql.Message[(inicio + separador.Length)..^1];
        var partes = clave.Split('.');
        return partes.Length == 1 && partes[0] == indice
            || partes.Length == 2 && partes[0].Equals(tabla, StringComparison.OrdinalIgnoreCase)
                && partes[1] == indice;
    }

    private static bool EsParqueEliminado(DbUpdateException error) =>
        error.InnerException is MySqlException mysql && mysql.Number == 1452
        && mysql.Message.Contains("CONSTRAINT `FK_Nodos_ParquesSolares_IdParque`", StringComparison.Ordinal);
}
