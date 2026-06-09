namespace ETHTPS.Ingestion.Rpc;

public class RpcSelector(RpcHealthTracker healthTracker)
{
    public string? SelectBest(string[] rpcUrls)
    {
        if (rpcUrls.Length == 0) return null;

        var ordered = rpcUrls
            .OrderByDescending(r => r.StartsWith("wss://", StringComparison.Ordinal))
            .ToArray();

        var available = ordered.FirstOrDefault(r => healthTracker.IsAvailable(r));
        if (available is not null) return available;

        // All in backoff — return the one with earliest BackoffUntil
        return ordered.MinBy(r => healthTracker.GetBackoffUntil(r));
    }
}
