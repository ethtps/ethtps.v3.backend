using Microsoft.Extensions.Logging;
using Npgsql;

namespace ETHTPS.Processing.Infrastructure;

public class SchemaInitializer(NpgsqlDataSource dataSource, ILogger<SchemaInitializer> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

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

        if (await TryCreateContinuousAggregateAsync(conn, "metrics_1m", "1 minute", ct))
            await TryAddAggregatePolicy(conn, "metrics_1m", "30 minutes", "1 minute", "1 minute", ct);
        if (await TryCreateContinuousAggregateAsync(conn, "metrics_5m", "5 minutes", ct))
            await TryAddAggregatePolicy(conn, "metrics_5m", "2 hours", "5 minutes", "5 minutes", ct);
        if (await TryCreateContinuousAggregateAsync(conn, "metrics_1h", "1 hour", ct))
            await TryAddAggregatePolicy(conn, "metrics_1h", "4 hours", "1 hour", "1 hour", ct);
        if (await TryCreateContinuousAggregateAsync(conn, "metrics_1d", "1 day", ct))
            await TryAddAggregatePolicy(conn, "metrics_1d", "4 days", "1 day", "1 day", ct);

        logger.LogInformation("Schema initialization complete");
    }

    // If the table is already a hypertable: no-op (data preserved).
    // If it's a plain table: drop it (wrong PK) and recreate + convert.
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
            // timescaledb_information not available — not TimescaleDB, fall back to plain CREATE TABLE IF NOT EXISTS
            await ExecuteAsync(conn, createSql.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS "), ct);
            return;
        }

        if (count > 0)
        {
            logger.LogDebug("Table {Table} is already a hypertable — skipping", table);
            return;
        }

        // Plain table exists (wrong PK) or doesn't exist — drop and recreate
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

    private async Task<bool> TryCreateContinuousAggregateAsync(NpgsqlConnection conn, string viewName, string bucket, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                CREATE MATERIALIZED VIEW IF NOT EXISTS {viewName}
                WITH (timescaledb.continuous) AS
                SELECT
                    chain_id,
                    time_bucket('{bucket}', timestamp) AS bucket,
                    avg(tps)  AS avg_tps,
                    max(tps)  AS max_tps,
                    avg(gps)  AS avg_gps,
                    max(gps)  AS max_gps,
                    count(*)  AS block_count
                FROM metrics
                WHERE tps IS NOT NULL
                GROUP BY chain_id, bucket
                WITH NO DATA;
                """;
            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Continuous aggregate {ViewName} creation skipped (TimescaleDB required)", viewName);
            return false;
        }
    }

    private async Task TryAddAggregatePolicy(NpgsqlConnection conn, string viewName,
        string startOffset, string endOffset, string scheduleInterval, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT add_continuous_aggregate_policy('{viewName}',
                    start_offset => INTERVAL '{startOffset}',
                    end_offset   => INTERVAL '{endOffset}',
                    schedule_interval => INTERVAL '{scheduleInterval}',
                    if_not_exists => TRUE);
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aggregate policy for {ViewName} skipped", viewName);
        }
    }
}
