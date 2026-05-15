using ETHTPS.Backfill.Models;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.Backfill.Repositories;

public class MetricsWriteRepository(NpgsqlDataSource dataSource)
{
    public async Task BulkInsertAsync(IReadOnlyList<ComputedMetrics> metrics, CancellationToken ct)
    {
        if (metrics.Count == 0) return;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        foreach (var m in metrics)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO metrics (chain_id, block_number, timestamp, tps, gps)
                VALUES ($1, $2, $3, $4, $5)
                ON CONFLICT (chain_id, block_number) DO NOTHING
                """;
            cmd.Parameters.Add(new NpgsqlParameter<int> { Value = m.ChainId });
            cmd.Parameters.Add(new NpgsqlParameter<long> { Value = m.BlockNumber });
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = m.Timestamp, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            if (m.Tps.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter<double> { Value = m.Tps.Value });
            else
                cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.Double });
            if (m.Gps.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter<double> { Value = m.Gps.Value });
            else
                cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.Double });
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
