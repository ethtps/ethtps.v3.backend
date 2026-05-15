using ETHTPS.Processing.Models;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.Processing.Repositories;

public class MetricsRepository(NpgsqlDataSource dataSource)
{
    public async Task InsertAsync(ComputedMetrics metrics, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO metrics (chain_id, block_number, timestamp, tps, gps)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (chain_id, block_number) DO NOTHING
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = metrics.ChainId });
        cmd.Parameters.Add(new NpgsqlParameter<long> { Value = metrics.BlockNumber });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = metrics.Timestamp, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        if (metrics.Tps.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter<double> { Value = metrics.Tps.Value });
        else
            cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.Double });
        if (metrics.Gps.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter<double> { Value = metrics.Gps.Value });
        else
            cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.Double });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
