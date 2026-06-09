namespace ETHTPS.Ingestion.Watchers;

public sealed class WatcherHandle(Task task, CancellationTokenSource cts) : IDisposable
{
    public Task Task { get; } = task;
    public CancellationTokenSource Cts { get; } = cts;

    public void Dispose() => Cts.Dispose();
}
