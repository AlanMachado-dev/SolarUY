using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.ParquesSolares;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class ParqueSolarRepository : IParqueSolarRepository
{
    private readonly SolarUyDbContext _context;

    public ParqueSolarRepository(SolarUyDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExisteNombreAsync(string claveNombre, CancellationToken cancellationToken = default) =>
        _context.ParquesSolares.AsNoTracking()
            .AnyAsync(parque => parque.Nombre.Clave == claveNombre, cancellationToken);

    public async Task<ResultadoGuardadoParqueSolar> GuardarAsync(ParqueSolar parque,
        CancellationToken cancellationToken = default)
    {
        _context.ParquesSolares.Add(parque);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return ResultadoGuardadoParqueSolar.Guardado;
        }
        catch (DbUpdateException error) when (EsConflictoDeNombre(error))
        {
            _context.Entry(parque.Nombre).State = EntityState.Detached;
            _context.Entry(parque).State = EntityState.Detached;
            return ResultadoGuardadoParqueSolar.ConflictoNombre;
        }
    }

    private static bool EsConflictoDeNombre(DbUpdateException error) =>
        error.InnerException is SqliteException sqlite
        && sqlite.SqliteExtendedErrorCode == 2067
        && sqlite.Message.Contains("UNIQUE constraint failed: ParquesSolares.ClaveNombre", StringComparison.Ordinal);
}
