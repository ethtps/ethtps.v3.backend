using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ETHTPS.ChainRegistry.Clients;

public partial class L2BeatClient(HttpClient httpClient, ILogger<L2BeatClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [GeneratedRegex(@"[^a-z0-9 ]")]
    private static partial Regex NonAlphanumeric();

    public async Task<IReadOnlyDictionary<string, string>?> FetchChainTypesAsync(CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.GetAsync("https://l2beat.com/api/scaling/summary", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var parsed = await JsonSerializer.DeserializeAsync<L2BeatResponse>(stream, JsonOptions, ct);
            if (parsed?.Projects is null) return null;

            var result = new Dictionary<string, string>();
            foreach (var project in parsed.Projects.Values)
            {
                var type = NormalizeCategory(project.Category);
                if (type == "mainnet") continue; // only store non-mainnet entries
                result[NormalizeName(project.Name)] = type;
                result[NormalizeName(project.Id)] = type; // also index by slug
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch chain types from L2Beat");
            return null;
        }
    }

    public static string? LookupNetworkType(string chainlistName, IReadOnlyDictionary<string, string> lookup)
    {
        var normalized = NormalizeName(chainlistName);

        // Exact match
        if (lookup.TryGetValue(normalized, out var exact)) return exact;

        // Check if the L2Beat name is fully contained in the chainlist name or vice versa
        foreach (var (key, type) in lookup)
        {
            if (normalized.Contains(key) || key.Contains(normalized))
                return type;
        }

        return null;
    }

    public static string NormalizeCategory(string? category) => category switch
    {
        "ZK Rollup" => "zk rollup",
        "Validium"  => "zk rollup",
        "Optimistic Rollup" => "optimistic rollup",
        "Optimium"  => "optimistic rollup",
        _ => "mainnet"
    };

    private static string NormalizeName(string name)
    {
        var lower = name.ToLowerInvariant().Replace('-', ' ');
        return NonAlphanumeric().Replace(lower, "").Trim();
    }
}
