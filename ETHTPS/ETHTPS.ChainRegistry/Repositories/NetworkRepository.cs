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
                chain_id          INTEGER PRIMARY KEY,
                name              TEXT        NOT NULL,
                rpc_urls          TEXT[]      NOT NULL,
                enabled           BOOLEAN     NOT NULL DEFAULT TRUE,
                is_testnet        BOOLEAN     NOT NULL DEFAULT FALSE,
                network_type      TEXT        NOT NULL DEFAULT 'mainnet',
                logo              BYTEA,
                logo_content_type TEXT,
                removed_at        TIMESTAMPTZ,
                last_synced_at    TIMESTAMPTZ NOT NULL
            );
            ALTER TABLE networks ADD COLUMN IF NOT EXISTS is_testnet        BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE networks ADD COLUMN IF NOT EXISTS network_type      TEXT    NOT NULL DEFAULT 'mainnet';
            ALTER TABLE networks ADD COLUMN IF NOT EXISTS logo                 BYTEA;
            ALTER TABLE networks ADD COLUMN IF NOT EXISTS logo_content_type   TEXT;
            ALTER TABLE networks ADD COLUMN IF NOT EXISTS logo_fetch_attempts SMALLINT NOT NULL DEFAULT 0;
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Network>> GetAllAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT chain_id, name, rpc_urls, enabled, is_testnet, network_type, logo IS NOT NULL, logo_fetch_attempts, removed_at, last_synced_at FROM networks";
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
                IsTestnet = reader.GetBoolean(4),
                NetworkType = reader.GetString(5),
                HasLogo = reader.GetBoolean(6),
                LogoFetchAttempts = reader.GetInt16(7),
                RemovedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                LastSyncedAt = reader.GetFieldValue<DateTimeOffset>(9)
            });
        }
        return networks;
    }

    public async Task UpsertAsync(Network network, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO networks (chain_id, name, rpc_urls, enabled, is_testnet, network_type, removed_at, last_synced_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (chain_id) DO UPDATE SET
                name           = EXCLUDED.name,
                rpc_urls       = EXCLUDED.rpc_urls,
                enabled        = EXCLUDED.enabled,
                is_testnet     = EXCLUDED.is_testnet,
                network_type   = EXCLUDED.network_type,
                removed_at     = EXCLUDED.removed_at,
                last_synced_at = EXCLUDED.last_synced_at
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = network.ChainId });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = network.Name });
        cmd.Parameters.Add(new NpgsqlParameter { Value = network.RpcUrls, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<bool> { Value = network.Enabled });
        cmd.Parameters.Add(new NpgsqlParameter<bool> { Value = network.IsTestnet });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = network.NetworkType });
        if (network.RemovedAt.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = network.RemovedAt.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        else
            cmd.Parameters.Add(new NpgsqlParameter { Value = DBNull.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { Value = network.LastSyncedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task IncrementLogoFetchAttemptsAsync(int chainId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE networks SET logo_fetch_attempts = logo_fetch_attempts + 1 WHERE chain_id = $1";
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateLogoAsync(int chainId, byte[] logo, string contentType, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE networks SET logo = $1, logo_content_type = $2 WHERE chain_id = $3 AND logo IS NULL";
        cmd.Parameters.Add(new NpgsqlParameter { Value = logo, NpgsqlDbType = NpgsqlDbType.Bytea });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = contentType });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
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
