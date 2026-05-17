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
        if (!ViewMap.TryGetValue(resolution, out var viewName))
            throw new ArgumentException($"Unknown resolution: {resolution}", nameof(resolution));

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT bucket, chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
            FROM {viewName}
            WHERE chain_id = $1 AND bucket >= $2 AND bucket <= $3
            ORDER BY bucket ASC
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = FloorToBucket(from, resolution), NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = FloorToBucket(to, resolution), NpgsqlDbType = NpgsqlDbType.TimestampTz });

        return await ReadBucketsAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<HistoricalBucket>> GetGlobalHistoryAsync(
        DateTimeOffset from, DateTimeOffset to, string resolution, CancellationToken ct)
    {
        if (!ViewMap.TryGetValue(resolution, out var viewName))
            throw new ArgumentException($"Unknown resolution: {resolution}", nameof(resolution));

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT bucket, chain_id, avg_tps, max_tps, avg_gps, max_gps, block_count
            FROM {viewName}
            WHERE bucket >= $1 AND bucket <= $2
            ORDER BY bucket ASC, chain_id ASC
            """;
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = FloorToBucket(from, resolution), NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = FloorToBucket(to, resolution), NpgsqlDbType = NpgsqlDbType.TimestampTz });

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
