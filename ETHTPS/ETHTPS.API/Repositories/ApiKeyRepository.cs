using Npgsql;

namespace ETHTPS.API.Repositories;

public record ApiKey(string KeyHash, string Name, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool Enabled);

public class ApiKeyRepository(NpgsqlDataSource dataSource)
{
    public async Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key_hash, name, created_at, expires_at, enabled FROM api_keys WHERE key_hash = $1";
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = keyHash });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ApiKey(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetBoolean(4));
    }
}
