using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LearningPlatform.Infrastructure.Health;

public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("select 1");
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
