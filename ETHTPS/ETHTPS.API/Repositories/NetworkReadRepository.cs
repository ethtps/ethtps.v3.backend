using ETHTPS.API.Models.Responses;
using Npgsql;

namespace ETHTPS.API.Repositories;

public class NetworkReadRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<NetworkResponse>> GetAllActiveAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT chain_id, name, rpc_urls, enabled, is_testnet, network_type FROM networks WHERE removed_at IS NULL ORDER BY chain_id";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<NetworkResponse>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new NetworkResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetFieldValue<string[]>(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4),
                reader.GetString(5)));
        }
        return list;
    }

    public async Task<NetworkResponse?> GetByChainIdAsync(int chainId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT chain_id, name, rpc_urls, enabled, is_testnet, network_type FROM networks WHERE chain_id = $1 AND removed_at IS NULL";
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = chainId });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new NetworkResponse(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetFieldValue<string[]>(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4),
            reader.GetString(5));
    }
}
