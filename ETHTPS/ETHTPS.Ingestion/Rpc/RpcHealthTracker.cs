using System.Collections.Concurrent;

namespace ETHTPS.Ingestion.Rpc;

public class RpcUrlState
{
    public int FailureCount { get; set; }
    public DateTimeOffset BackoffUntil { get; set; } = DateTimeOffset.MinValue;
}

public class RpcHealthTracker
{
    private readonly ConcurrentDictionary<string, RpcUrlState> _states = new();

    private RpcUrlState GetOrCreate(string url) =>
        _states.GetOrAdd(url, _ => new RpcUrlState());

    public void RecordSuccess(string url)
    {
        var state = GetOrCreate(url);
        state.FailureCount = 0;
        state.BackoffUntil = DateTimeOffset.MinValue;
    }

    public void RecordFailure(string url, int maxBackoffSeconds)
    {
        var state = GetOrCreate(url);
        state.FailureCount++;
        var backoffSeconds = Math.Min(Math.Pow(2, state.FailureCount), maxBackoffSeconds);
        state.BackoffUntil = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);
    }

    public bool IsAvailable(string url) =>
        GetOrCreate(url).BackoffUntil < DateTimeOffset.UtcNow;

    public DateTimeOffset GetBackoffUntil(string url) =>
        GetOrCreate(url).BackoffUntil;
}
