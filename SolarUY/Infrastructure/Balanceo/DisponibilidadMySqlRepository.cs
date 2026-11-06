using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SolarUY.Infrastructure.Persistencia;

namespace SolarUY.Infrastructure.Balanceo;

public sealed class DisponibilidadMySqlRepository(IDbContextFactory<SolarUyDbContext> fabrica) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var contexto = await fabrica.CreateDbContextAsync(cancellationToken);
            contexto.Database.SetCommandTimeout(2);
            await contexto.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("mysql_no_disponible");
        }
    }
}
