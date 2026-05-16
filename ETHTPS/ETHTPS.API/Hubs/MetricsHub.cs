using ETHTPS.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace ETHTPS.API.Hubs;

public class MetricsHub(ConnectionTracker tracker) : Hub
{
    public override Task OnConnectedAsync()
    {
        tracker.OnConnected();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        tracker.OnDisconnected();
        return base.OnDisconnectedAsync(exception);
    }

    public async Task Subscribe(int[] chainIds)
    {
        foreach (var id in chainIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chain:{id}");
    }

    public async Task Unsubscribe(int[] chainIds)
    {
        foreach (var id in chainIds)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chain:{id}");
    }
}
