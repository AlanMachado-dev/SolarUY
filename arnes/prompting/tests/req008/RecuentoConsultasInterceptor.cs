using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class RecuentoConsultasInterceptor : DbCommandInterceptor
{
    private int _cantidad;
    public int Cantidad => Volatile.Read(ref _cantidad);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _cantidad);
        return ValueTask.FromResult(result);
    }
}
