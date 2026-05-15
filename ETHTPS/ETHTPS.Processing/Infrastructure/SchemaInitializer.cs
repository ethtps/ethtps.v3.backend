using Microsoft.Extensions.Logging;
using Npgsql;

namespace ETHTPS.Processing.Infrastructure;

public class SchemaInitializer(NpgsqlDataSource dataSource, ILogger<SchemaInitializer> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

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

        await TryCreateContinuousAggregateAsync(conn, "metrics_1m", "1 minute", ct);
        await TryCreateContinuousAggregateAsync(conn, "metrics_5m", "5 minutes", ct);
        await TryCreateContinuousAggregateAsync(conn, "metrics_1h", "1 hour", ct);
        await TryCreateContinuousAggregateAsync(conn, "metrics_1d", "1 day", ct);

        await TryAddAggregatePolicy(conn, "metrics_1m", "2 minutes", "1 minute", "1 minute", ct);
        await TryAddAggregatePolicy(conn, "metrics_5m", "10 minutes", "5 minutes", "5 minutes", ct);
        await TryAddAggregatePolicy(conn, "metrics_1h", "2 hours", "1 hour", "1 hour", ct);
        await TryAddAggregatePolicy(conn, "metrics_1d", "2 days", "1 day", "1 day", ct);

        logger.LogInformation("Schema initialization complete");
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
            logger.LogWarning(ex, "create_hypertable for {Table} skipped (TimescaleDB may not be installed)", table);
        }
    }

    private async Task TryCreateContinuousAggregateAsync(NpgsqlConnection conn, string viewName, string bucket, CancellationToken ct)
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
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Continuous aggregate {ViewName} creation skipped", viewName);
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
