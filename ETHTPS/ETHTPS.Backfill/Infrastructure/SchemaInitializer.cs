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

        await EnsureHypertableAsync(conn, "blocks", "timestamp", """
            CREATE TABLE blocks (
                chain_id      INTEGER      NOT NULL,
                block_number  BIGINT       NOT NULL,
                block_hash    TEXT         NOT NULL,
                timestamp     TIMESTAMPTZ  NOT NULL,
                tx_count      INTEGER      NOT NULL,
                gas_used      NUMERIC      NOT NULL,
                gas_limit     NUMERIC      NOT NULL,
                block_time_ms BIGINT       NOT NULL,
                ingested_at   TIMESTAMPTZ  NOT NULL,
                PRIMARY KEY (chain_id, block_number, timestamp)
            );
            """, ct);

        await EnsureHypertableAsync(conn, "transactions", "block_timestamp", """
            CREATE TABLE transactions (
                chain_id         INTEGER      NOT NULL,
                block_number     BIGINT       NOT NULL,
                block_hash       TEXT         NOT NULL,
                tx_hash          TEXT         NOT NULL,
                gas              NUMERIC      NOT NULL,
                block_timestamp  TIMESTAMPTZ  NOT NULL,
                PRIMARY KEY (chain_id, tx_hash, block_timestamp)
            );
            """, ct);

        await EnsureHypertableAsync(conn, "metrics", "timestamp", """
            CREATE TABLE metrics (
                chain_id      INTEGER           NOT NULL,
                block_number  BIGINT            NOT NULL,
                timestamp     TIMESTAMPTZ       NOT NULL,
                tps           DOUBLE PRECISION,
                gps           DOUBLE PRECISION,
                PRIMARY KEY (chain_id, block_number, timestamp)
            );
            """, ct);

        logger.LogInformation("Backfill schema initialization complete");
    }

    private async Task EnsureHypertableAsync(NpgsqlConnection conn, string table, string timeCol, string createSql, CancellationToken ct)
    {
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = $"""
            SELECT COUNT(*) FROM timescaledb_information.hypertables
            WHERE hypertable_name = '{table}';
            """;

        long count;
        try
        {
            count = (long)(await checkCmd.ExecuteScalarAsync(ct))!;
        }
        catch
        {
            await ExecuteAsync(conn, createSql.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS "), ct);
            return;
        }

        if (count > 0)
        {
            logger.LogDebug("Table {Table} is already a hypertable — skipping", table);
            return;
        }

        await ExecuteAsync(conn, $"DROP TABLE IF EXISTS {table} CASCADE;", ct);
        await ExecuteAsync(conn, createSql, ct);

        try
        {
            await using var htCmd = conn.CreateCommand();
            htCmd.CommandText = $"""
                SELECT create_hypertable('{table}', '{timeCol}',
                    partitioning_column => 'chain_id',
                    number_partitions => 16,
                    if_not_exists => TRUE);
                """;
            await htCmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Hypertable {Table} created", table);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "create_hypertable for {Table} skipped", table);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
