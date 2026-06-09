using ETHTPS.Processing.Models;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.Processing.Repositories;

public class TransactionRepository(NpgsqlDataSource dataSource)
{
    public async Task BulkInsertAsync(IReadOnlyList<RawTransaction> transactions, CancellationToken ct)
    {
        if (transactions.Count == 0) return;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY transactions (chain_id, block_number, block_hash, tx_hash, gas, block_timestamp) FROM STDIN (FORMAT BINARY)", ct);

            foreach (var tx in transactions)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(tx.ChainId, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(tx.BlockNumber, NpgsqlDbType.Bigint, ct);
                await writer.WriteAsync(tx.BlockHash, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(tx.TxHash, NpgsqlDbType.Text, ct);
                await writer.WriteAsync((decimal)tx.Gas, NpgsqlDbType.Numeric, ct);
                await writer.WriteAsync(tx.BlockTimestamp, NpgsqlDbType.TimestampTz, ct);
            }

            await writer.CompleteAsync(ct);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            // Unique violation — fall back to individual inserts
            await FallbackInsertAsync(conn, transactions, ct);
        }
    }

    private static async Task FallbackInsertAsync(NpgsqlConnection conn, IReadOnlyList<RawTransaction> transactions, CancellationToken ct)
    {
        foreach (var tx in transactions)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO transactions (chain_id, block_number, block_hash, tx_hash, gas, block_timestamp)
                VALUES ($1, $2, $3, $4, $5, $6)
                ON CONFLICT (chain_id, tx_hash, block_timestamp) DO NOTHING
                """;
            cmd.Parameters.Add(new NpgsqlParameter<int> { Value = tx.ChainId });
            cmd.Parameters.Add(new NpgsqlParameter<long> { Value = tx.BlockNumber });
            cmd.Parameters.Add(new NpgsqlParameter<string> { Value = tx.BlockHash });
            cmd.Parameters.Add(new NpgsqlParameter<string> { Value = tx.TxHash });
            cmd.Parameters.Add(new NpgsqlParameter<decimal> { Value = (decimal)tx.Gas });
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = tx.BlockTimestamp, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
