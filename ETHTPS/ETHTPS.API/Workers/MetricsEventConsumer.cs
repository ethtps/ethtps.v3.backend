using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.API.Hubs;
using ETHTPS.API.Models;
using ETHTPS.API.Models.Messages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ETHTPS.API.Workers;

public class MetricsEventConsumer(
    IHubContext<MetricsHub> hubContext,
    IConfiguration configuration,
    ILogger<MetricsEventConsumer> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<int, (double Tps, double Gps, DateTimeOffset Timestamp)> _liveState = new();

    public (double TotalTps, double TotalGps, int ActiveChains, DateTimeOffset ComputedAt) GetGlobalSnapshot()
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-60);
        var active = _liveState.Values.Where(v => v.Timestamp >= cutoff).ToList();
        return (
            active.Sum(v => v.Tps),
            active.Sum(v => v.Gps),
            active.Count,
            active.Count > 0 ? active.Max(v => v.Timestamp) : DateTimeOffset.UtcNow
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

                    // Push to SignalR
                    var message = new MetricsUpdateMessage(evt.ChainId, evt.Tps, evt.Gps, evt.BlockNumber, evt.Timestamp);
                    await hubContext.Clients
                        .Group($"chain:{evt.ChainId}")
                        .SendAsync("MetricsUpdate", message, stoppingToken);

                    consumer.Commit(result);

                    logger.LogDebug("Pushed metrics for chain {ChainId} block {BlockNumber}", evt.ChainId, evt.BlockNumber);
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
    }
}
