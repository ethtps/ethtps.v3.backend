using ETHTPS.ChainRegistry.Models;
using Npgsql;
using NpgsqlTypes;

namespace ETHTPS.ChainRegistry.Repositories;

public class NetworkRepository(NpgsqlDataSource dataSource) : INetworkRepository
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS networks (
                chain_id       INTEGER PRIMARY KEY,
                name           TEXT        NOT NULL,
                rpc_urls       TEXT[]      NOT NULL,
                enabled        BOOLEAN     NOT NULL DEFAULT TRUE,
                removed_at     TIMESTAMPTZ,
                last_synced_at TIMESTAMPTZ NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Network>> GetAllAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT chain_id, name, rpc_urls, enabled, removed_at, last_synced_at FROM networks";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var networks = new List<Network>();
        while (await reader.ReadAsync(ct))
        {
            networks.Add(new Network
            {
                ChainId = reader.GetInt32(0),
                Name = reader.GetString(1),
                RpcUrls = reader.GetFieldValue<string[]>(2),
                Enabled = reader.GetBoolean(3),
                RemovedAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                LastSyncedAt = reader.GetFieldValue<DateTimeOffset>(5)
            });
        }
        return networks;
    }

    public async Task UpsertAsync(Network network, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO networks (chain_id, name, rpc_urls, enabled, removed_at, last_synced_at)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (chain_id) DO UPDATE SET
                name           = EXCLUDED.name,
                rpc_urls       = EXCLUDED.rpc_urls,
                enabled        = EXCLUDED.enabled,
                removed_at     = EXCLUDED.removed_at,
                last_synced_at = EXCLUDED.last_synced_at
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = network.ChainId });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = network.Name });
        cmd.Parameters.Add(new NpgsqlParameter { Value = network.RpcUrls, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<bool> { Value = network.Enabled });
        if (network.RemovedAt.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = network.RemovedAt.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        else
            cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = network.LastSyncedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkRemovedAsync(int chainId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE networks SET removed_at = $1 WHERE chain_id = $2";
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = DateTimeOffset.UtcNow, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
