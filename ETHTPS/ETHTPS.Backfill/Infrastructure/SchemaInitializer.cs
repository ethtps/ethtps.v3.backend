using Microsoft.Extensions.Logging;
using Npgsql;

namespace ETHTPS.Backfill.Infrastructure;

public class SchemaInitializer(NpgsqlDataSource dataSource, ILogger<SchemaInitializer> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS backfill_progress (
                chain_id           INTEGER      PRIMARY KEY,
                last_block_number  BIGINT       NOT NULL DEFAULT -1,
                target_block       BIGINT,
                status             TEXT         NOT NULL DEFAULT 'pending',
                started_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
                updated_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
                completed_at       TIMESTAMPTZ
            );
            """, ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS blocks (
                chain_id      INTEGER      NOT NULL,
                block_number  BIGINT       NOT NULL,
                block_hash    TEXT         NOT NULL,
                timestamp     TIMESTAMPTZ  NOT NULL,
                tx_count      INTEGER      NOT NULL,
                gas_used      NUMERIC      NOT NULL,
                gas_limit     NUMERIC      NOT NULL,
                block_time_ms BIGINT       NOT NULL,
                ingested_at   TIMESTAMPTZ  NOT NULL,
                PRIMARY KEY (chain_id, block_number)
            );
            """, ct);

        await TryCreateHypertableAsync(conn, "blocks", "timestamp", ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS transactions (
                chain_id         INTEGER      NOT NULL,
                block_number     BIGINT       NOT NULL,
                block_hash       TEXT         NOT NULL,
                tx_hash          TEXT         NOT NULL,
                gas              NUMERIC      NOT NULL,
                block_timestamp  TIMESTAMPTZ  NOT NULL,
                PRIMARY KEY (chain_id, tx_hash)
            );
            """, ct);

        await TryCreateHypertableAsync(conn, "transactions", "block_timestamp", ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS metrics (
                chain_id      INTEGER           NOT NULL,
                block_number  BIGINT            NOT NULL,
                timestamp     TIMESTAMPTZ       NOT NULL,
                tps           DOUBLE PRECISION,
                gps           DOUBLE PRECISION,
                PRIMARY KEY (chain_id, block_number)
            );
            """, ct);

        await TryCreateHypertableAsync(conn, "metrics", "timestamp", ct);

        logger.LogInformation("Backfill schema initialization complete");
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task TryCreateHypertableAsync(NpgsqlConnection conn, string table, string timeCol, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT create_hypertable('{table}', '{timeCol}',
                    partitioning_column => 'chain_id',
                    number_partitions => 16,
                    if_not_exists => TRUE);
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "create_hypertable for {Table} skipped", table);
        }
    }
}
