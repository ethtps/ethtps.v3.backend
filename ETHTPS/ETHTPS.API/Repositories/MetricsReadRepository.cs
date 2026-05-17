using ETHTPS.API.Models.Responses;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.API.Repositories;

public class MetricsReadRepository(NpgsqlDataSource dataSource)
{
    private static readonly Dictionary<string, string> ViewMap = new()
    {
        ["1s"] = "metrics_1s",
        ["1m"] = "metrics_1m",
        ["5m"] = "metrics_5m",
        ["1h"] = "metrics_1h",
        ["1d"] = "metrics_1d"
    };

    private static readonly Dictionary<string, TimeSpan> BucketSize = new()
    {
        ["1s"] = TimeSpan.FromSeconds(1),
        ["1m"] = TimeSpan.FromMinutes(1),
        ["5m"] = TimeSpan.FromMinutes(5),
        ["1h"] = TimeSpan.FromHours(1),
        ["1d"] = TimeSpan.FromDays(1)
    };

    private static DateTimeOffset FloorToBucket(DateTimeOffset dt, string resolution)
    {
        var size = BucketSize[resolution];
        var ticks = dt.UtcTicks / size.Ticks * size.Ticks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    public async Task<IReadOnlyList<HistoricalBucket>> GetHistoryAsync(
        int chainId, DateTimeOffset from, DateTimeOffset to, string resolution, CancellationToken ct)
    {
        if (!ViewMap.TryGetValue(resolution, out _))
            throw new ArgumentException($"Unknown resolution: {resolution}", nameof(resolution));

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var flooredFrom = FloorToBucket(from, resolution);
        var flooredTo = FloorToBucket(to, resolution);

        if (resolution == "1s")
        {
            cmd.CommandText = """
                WITH series AS (
                    SELECT generate_series($2::timestamptz, $3::timestamptz, INTERVAL '1 second') AS bucket
                ),
                data AS (
                    SELECT bucket, avg_tps, max_tps, avg_gps, max_gps, block_count
                    FROM metrics_1s
                    WHERE chain_id = $1 AND bucket >= $2 AND bucket <= $3
                ),
                joined AS (
                    SELECT s.bucket, d.avg_tps, d.max_tps, d.avg_gps, d.max_gps,
                           COALESCE(d.block_count, 0) AS block_count
                    FROM series s
                    LEFT JOIN data d ON d.bucket = s.bucket
                ),
                grps AS (
                    SELECT *,
                        count(avg_tps) OVER (ORDER BY bucket ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp
                    FROM joined
                ),
                filled AS (
                    SELECT bucket,
                        first_value(avg_tps) OVER (PARTITION BY grp ORDER BY bucket) AS avg_tps,
                        first_value(max_tps) OVER (PARTITION BY grp ORDER BY bucket) AS max_tps,
                        first_value(avg_gps) OVER (PARTITION BY grp ORDER BY bucket) AS avg_gps,
                        first_value(max_gps) OVER (PARTITION BY grp ORDER BY bucket) AS max_gps,
                        block_count
                    FROM grps
                )
                SELECT bucket, $1 AS chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
                FROM filled
                ORDER BY bucket ASC
                """;
        }
        else
        {
            var viewName = ViewMap[resolution];
            cmd.CommandText = $"""
                SELECT bucket, chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
                FROM {viewName}
                WHERE chain_id = $1 AND bucket >= $2 AND bucket <= $3
                ORDER BY bucket ASC
                """;
        }

        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = flooredFrom, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = flooredTo, NpgsqlDbType = NpgsqlDbType.TimestampTz });

        return await ReadBucketsAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<HistoricalBucket>> GetGlobalHistoryAsync(
        DateTimeOffset from, DateTimeOffset to, string resolution, CancellationToken ct)
    {
        if (!ViewMap.TryGetValue(resolution, out _))
            throw new ArgumentException($"Unknown resolution: {resolution}", nameof(resolution));

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var flooredFrom = FloorToBucket(from, resolution);
        var flooredTo = FloorToBucket(to, resolution);

        if (resolution == "1s")
        {
            cmd.CommandText = """
                WITH chains AS (
                    SELECT DISTINCT chain_id FROM metrics_1s
                    WHERE bucket >= $1 AND bucket <= $2
                ),
                series AS (
                    SELECT c.chain_id, generate_series($1::timestamptz, $2::timestamptz, INTERVAL '1 second') AS bucket
                    FROM chains c
                ),
                data AS (
                    SELECT bucket, chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
                    FROM metrics_1s
                    WHERE bucket >= $1 AND bucket <= $2
                ),
                joined AS (
                    SELECT s.bucket, s.chain_id, d.avg_tps, d.max_tps, d.avg_gps, d.max_gps,
                           COALESCE(d.block_count, 0) AS block_count
                    FROM series s
                    LEFT JOIN data d ON d.bucket = s.bucket AND d.chain_id = s.chain_id
                ),
                grps AS (
                    SELECT *,
                        count(avg_tps) OVER (PARTITION BY chain_id ORDER BY bucket ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp
                    FROM joined
                ),
                filled AS (
                    SELECT bucket, chain_id,
                        first_value(avg_tps) OVER (PARTITION BY chain_id, grp ORDER BY bucket) AS avg_tps,
                        first_value(max_tps) OVER (PARTITION BY chain_id, grp ORDER BY bucket) AS max_tps,
                        first_value(avg_gps) OVER (PARTITION BY chain_id, grp ORDER BY bucket) AS avg_gps,
                        first_value(max_gps) OVER (PARTITION BY chain_id, grp ORDER BY bucket) AS max_gps,
                        block_count
                    FROM grps
                )
                SELECT bucket, chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
                FROM filled
                ORDER BY bucket ASC, chain_id ASC
                """;
        }
        else
        {
            var viewName = ViewMap[resolution];
            cmd.CommandText = $"""
                SELECT bucket, chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
                FROM {viewName}
                WHERE bucket >= $1 AND bucket <= $2
                ORDER BY bucket ASC, chain_id ASC
                """;
        }

        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = flooredFrom, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = flooredTo, NpgsqlDbType = NpgsqlDbType.TimestampTz });

        return await ReadBucketsAsync(cmd, ct);
    }

    private static async Task<IReadOnlyList<HistoricalBucket>> ReadBucketsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<HistoricalBucket>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new HistoricalBucket(
                reader.GetFieldValue<DateTimeOffset>(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6))
            ));
        }
        return list;
    }
}
