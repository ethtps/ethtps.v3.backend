namespace ETHTPS.API.Services;

public sealed class ConnectionTracker
{
    private int _count;

    public void OnConnected() => Interlocked.Increment(ref _count);
    public void OnDisconnected() => Interlocked.Decrement(ref _count);
    public int Count => _count;
}
