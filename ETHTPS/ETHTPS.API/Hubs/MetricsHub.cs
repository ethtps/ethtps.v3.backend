using Microsoft.AspNetCore.SignalR;

namespace ETHTPS.API.Hubs;

public class MetricsHub : Hub
{
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
