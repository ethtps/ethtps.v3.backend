using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.API.Hubs;
using ETHTPS.API.Models;
using ETHTPS.API.Models.Messages;
using ETHTPS.API.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ETHTPS.API.Workers;

public class MetricsEventConsumer(
    IHubContext<MetricsHub> hubContext,
    IConfiguration configuration,
    NetworkReadRepository networkRepository,
    ILogger<MetricsEventConsumer> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<int, (double Tps, double Gps, DateTimeOffset Timestamp)> _liveState = new();
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastPushed = new();
    private readonly ConcurrentDictionary<int, (bool IsTestnet, string NetworkType)> _networkMeta = new();

    private async Task RefreshNetworkMetaAsync(CancellationToken ct)
    {
        try
        {
            var networks = await networkRepository.GetAllActiveAsync(ct);
            foreach (var n in networks)
                _networkMeta[n.ChainId] = (n.IsTestnet, n.NetworkType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh network metadata");
        }
    }

    private async Task RefreshNetworkMetaPeriodicallyAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await RefreshNetworkMetaAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    public (double TotalTps, double TotalGps, int ActiveChains, DateTimeOffset ComputedAt) GetGlobalSnapshot(
        bool includeTestnets = true, bool includeSidechains = true)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-60);
        var active = _liveState
            .Where(kv =>
            {
                if (kv.Value.Timestamp < cutoff) return false;
                if (_networkMeta.TryGetValue(kv.Key, out var meta))
                {
                    if (!includeTestnets && meta.IsTestnet) return false;
                    if (!includeSidechains && meta.NetworkType == "sidechain") return false;
                }
                return true;
            })
            .Select(kv => kv.Value)
            .ToList();
        return (
            active.Sum(v => v.Tps),
            active.Sum(v => v.Gps),
            active.Count,
            active.Count > 0 ? active.Max(v => v.Timestamp) : DateTimeOffset.UtcNow
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshNetworkMetaAsync(stoppingToken);
        var metaTask = RefreshNetworkMetaPeriodicallyAsync(stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "api-signalr",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("aggregated-metrics");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error consuming aggregated-metrics");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                if (result?.Message?.Value is null) continue;

                try
                {
                    var evt = JsonSerializer.Deserialize<MetricsComputedEvent>(result.Message.Value);
                    if (evt is null) continue;

                    // Update in-memory state
                    if (evt.Tps.HasValue && evt.Gps.HasValue)
                        _liveState[evt.ChainId] = (evt.Tps.Value, evt.Gps.Value, evt.Timestamp);

                    // Push to SignalR — throttled to one update per second per chain
                    var now = DateTimeOffset.UtcNow;
                    if (!_lastPushed.TryGetValue(evt.ChainId, out var lastPush) ||
                        now - lastPush >= TimeSpan.FromSeconds(1))
                    {
                        var message = new MetricsUpdateMessage(evt.ChainId, evt.Tps, evt.Gps, evt.BlockNumber, evt.Timestamp);
                        await hubContext.Clients
                            .Group($"chain:{evt.ChainId}")
                            .SendAsync("MetricsUpdate", message, stoppingToken);
                        _lastPushed[evt.ChainId] = now;
                    }

                    consumer.Commit(result);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error processing metrics event");
                }
            }
        }
        finally
        {
            consumer.Close();
        }

        await metaTask;
    }
}
