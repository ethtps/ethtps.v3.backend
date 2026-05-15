using System.Text.Json;
using ETHTPS.ChainRegistry.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.ChainRegistry.Clients;

public class ChainlistClient(HttpClient httpClient, IOptions<ChainRegistryOptions> options, ILogger<ChainlistClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ChainlistNetwork>?> FetchNetworksAsync(CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.GetAsync(options.Value.ChainlistUrl, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<List<ChainlistNetwork>>(stream, JsonOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch networks from chainlist");
            return null;
        }
    }

    public static string[] FilterUsableRpcs(string[] rpcs) =>
        rpcs
            .Where(r =>
                (r.StartsWith("https://", StringComparison.Ordinal) || r.StartsWith("wss://", StringComparison.Ordinal))
                && !r.Contains("${", StringComparison.Ordinal)
                && !r.Contains("API_KEY", StringComparison.Ordinal)
                && !r.Contains("api-key", StringComparison.Ordinal))
            .Distinct()
            .OrderByDescending(r => r.StartsWith("wss://", StringComparison.Ordinal))
            .ToArray();
}
