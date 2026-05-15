using ETHTPS.Backfill.Models;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.Backfill.Repositories;

public class BlockWriteRepository(NpgsqlDataSource dataSource)
{
    public async Task BulkInsertAsync(IReadOnlyList<RawBlock> blocks, CancellationToken ct)
    {
        if (blocks.Count == 0) return;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        foreach (var block in blocks)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO blocks (chain_id, block_number, block_hash, timestamp, tx_count, gas_used, gas_limit, block_time_ms, ingested_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                ON CONFLICT (chain_id, block_number) DO NOTHING
                """;
            cmd.Parameters.Add(new NpgsqlParameter<int> { Value = block.ChainId });
            cmd.Parameters.Add(new NpgsqlParameter<long> { Value = block.BlockNumber });
            cmd.Parameters.Add(new NpgsqlParameter<string> { Value = block.BlockHash });
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = block.Timestamp, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            cmd.Parameters.Add(new NpgsqlParameter<int> { Value = block.TransactionCount });
            cmd.Parameters.Add(new NpgsqlParameter<decimal> { Value = (decimal)block.GasUsed });
            cmd.Parameters.Add(new NpgsqlParameter<decimal> { Value = (decimal)block.GasLimit });
            cmd.Parameters.Add(new NpgsqlParameter<long> { Value = block.BlockTimeMs });
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = block.IngestedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<DateTimeOffset?> GetTimestampAsync(int chainId, long blockNumber, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT timestamp FROM blocks WHERE chain_id = $1 AND block_number = $2";
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        cmd.Parameters.Add(new NpgsqlParameter<long> { Value = blockNumber });
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : (DateTimeOffset)result;
    }
}
