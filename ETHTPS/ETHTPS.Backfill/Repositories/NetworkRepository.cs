using ETHTPS.Backfill.Models;
using Npgsql;

namespace ETHTPS.Backfill.Repositories;

public class NetworkRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<Network>> GetAllInShardAsync(int minChainId, int maxChainId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT chain_id, name, rpc_urls FROM networks
            WHERE removed_at IS NULL
              AND chain_id BETWEEN $1 AND $2
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = minChainId });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = maxChainId });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var networks = new List<Network>();
        while (await reader.ReadAsync(ct))
        {
            networks.Add(new Network
            {
                ChainId = reader.GetInt32(0),
                Name = reader.GetString(1),
                RpcUrls = reader.GetFieldValue<string[]>(2)
            });
        }
        return networks;
    }
}
