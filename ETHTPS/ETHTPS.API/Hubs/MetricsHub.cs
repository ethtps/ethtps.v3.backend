using ETHTPS.API.Repositories;
using ETHTPS.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace ETHTPS.API.Hubs;

public class MetricsHub(ConnectionTracker tracker, NetworkReadRepository networkRepository) : Hub
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

    public async Task SubscribeAll(bool includeTestnets = true, bool includeSidechains = true)
    {
        var networks = await networkRepository.GetAllActiveAsync(Context.ConnectionAborted);
        foreach (var network in networks)
        {
            if (!includeTestnets && network.IsTestnet) continue;
            if (!includeSidechains && network.NetworkType == "sidechain") continue;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chain:{network.ChainId}");
        }
    }

    public async Task Unsubscribe(int[] chainIds)
    {
        foreach (var id in chainIds)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chain:{id}");
    }
}
