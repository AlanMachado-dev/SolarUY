using MySql.Data.MySqlClient;
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
        error.InnerException is MySqlException mysql
        && mysql.Number == 1062
        && EsIndiceDeNombre(mysql.Message);

    private static bool EsIndiceDeNombre(string mensaje)
    {
        // MySQL 8.4 informa el identificador al final, separado del valor duplicado.
        const string separador = " for key '";
        var inicio = mensaje.LastIndexOf(separador, StringComparison.Ordinal);
        if (inicio < 0 || !mensaje.EndsWith("'", StringComparison.Ordinal))
            return false;
        var indice = mensaje[(inicio + separador.Length)..^1];
        var partes = indice.Split('.');
        return partes.Length == 1 && partes[0] == "UX_ParquesSolares_ClaveNombre"
            || partes.Length == 2
                && partes[0].Equals("ParquesSolares", StringComparison.OrdinalIgnoreCase)
                && partes[1] == "UX_ParquesSolares_ClaveNombre";
    }
}
