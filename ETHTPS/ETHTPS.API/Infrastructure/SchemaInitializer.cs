using Microsoft.Extensions.Logging;
using Npgsql;

namespace ETHTPS.API.Infrastructure;

public class SchemaInitializer(NpgsqlDataSource dataSource, ILogger<SchemaInitializer> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS api_keys (
                key_hash    TEXT        PRIMARY KEY,
                name        TEXT        NOT NULL,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                expires_at  TIMESTAMPTZ,
                enabled     BOOLEAN     NOT NULL DEFAULT TRUE
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("API schema initialization complete");
    }
}
