namespace ETHTPS.Backfill.Rpc;

public class RpcSelector(RpcHealthTracker healthTracker)
{
    public string? SelectBest(string[] rpcUrls)
    {
        if (rpcUrls.Length == 0) return null;

        var ordered = rpcUrls
            .Where(r => r.StartsWith("https://", StringComparison.Ordinal))
            .ToArray();

        if (ordered.Length == 0) ordered = rpcUrls;

        var available = ordered.FirstOrDefault(r => healthTracker.IsAvailable(r));
        if (available is not null) return available;

        return ordered.MinBy(r => healthTracker.GetBackoffUntil(r));
    }
}
