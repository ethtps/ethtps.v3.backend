using ETHTPS.Backfill.Models;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.Backfill.Repositories;

public class BackfillProgressRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<BackfillProgress>> GetAllAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT chain_id, last_block_number, target_block, status, started_at, updated_at, completed_at FROM backfill_progress";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BackfillProgress>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new BackfillProgress
            {
                ChainId = reader.GetInt32(0),
                LastBlockNumber = reader.GetInt64(1),
                TargetBlock = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                Status = reader.GetString(3),
                StartedAt = reader.GetFieldValue<DateTimeOffset>(4),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(5),
                CompletedAt = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)
            });
        }
        return list;
    }

    public async Task UpsertAsync(BackfillProgress progress, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO backfill_progress (chain_id, last_block_number, target_block, status, started_at, updated_at, completed_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT (chain_id) DO UPDATE SET
                last_block_number = EXCLUDED.last_block_number,
                target_block      = EXCLUDED.target_block,
                status            = EXCLUDED.status,
                updated_at        = EXCLUDED.updated_at,
                completed_at      = EXCLUDED.completed_at
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = progress.ChainId });
        cmd.Parameters.Add(new NpgsqlParameter<long> { Value = progress.LastBlockNumber });
        if (progress.TargetBlock.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter<long> { Value = progress.TargetBlock.Value });
        else
            cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bigint });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = progress.Status });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = progress.StartedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = progress.UpdatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        if (progress.CompletedAt.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = progress.CompletedAt.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        else
            cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateCheckpointAsync(int chainId, long lastBlockNumber, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE backfill_progress SET last_block_number = $1, updated_at = $2 WHERE chain_id = $3";
        cmd.Parameters.Add(new NpgsqlParameter<long> { Value = lastBlockNumber });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = DateTimeOffset.UtcNow, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkCompletedAsync(int chainId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE backfill_progress SET status = 'completed', completed_at = $1, updated_at = $1 WHERE chain_id = $2";
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = DateTimeOffset.UtcNow, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkRunningAsync(int chainId, long targetBlock, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE backfill_progress SET status = 'running', target_block = $1, updated_at = $2 WHERE chain_id = $3";
        cmd.Parameters.Add(new NpgsqlParameter<long> { Value = targetBlock });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = DateTimeOffset.UtcNow, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
