using System.Text.Json;
using ETHTPS.Ingestion.Models;
using Microsoft.Extensions.Logging;

namespace ETHTPS.Ingestion.RpcOverrides;

public class RpcOverridesLoader(ILogger<RpcOverridesLoader> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string FileName = "RPCOverrides.json";

    public IReadOnlyDictionary<string, string[]> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (!File.Exists(path))
        {
            logger.LogInformation("No {File} found — no RPC overrides applied", FileName);
            return new Dictionary<string, string[]>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var overrides = JsonSerializer.Deserialize<List<RpcOverride>>(json, JsonOptions) ?? [];
            var result = overrides
                .Where(o => !string.IsNullOrWhiteSpace(o.NetworkName) && o.RpcUrls.Length > 0)
                .ToDictionary(
                    o => o.NetworkName.ToLowerInvariant(),
                    o => o.RpcUrls);
            logger.LogInformation("Loaded RPC overrides for {Count} network(s) from {File}", result.Count, FileName);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load {File} — no RPC overrides applied", FileName);
            return new Dictionary<string, string[]>();
        }
    }
}
