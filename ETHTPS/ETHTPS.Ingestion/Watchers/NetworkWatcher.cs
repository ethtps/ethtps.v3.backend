using ETHTPS.Ingestion.Models;
using ETHTPS.Ingestion.Options;
using ETHTPS.Ingestion.Publishers;
using ETHTPS.Ingestion.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Ingestion.Watchers;

public class NetworkWatcher(
    Network network,
    RpcClient rpcClient,
    RpcHealthTracker healthTracker,
    RpcSelector rpcSelector,
    IBlockPublisher publisher,
    IOptions<IngestionOptions> options,
    StatusTracker statusTracker,
    ILogger<NetworkWatcher> logger)
{
    private DateTimeOffset? _previousBlockTimestamp;
    private string? _lastSeenBlockHash;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var rpc = rpcSelector.SelectBest(network.RpcUrls);
            if (rpc is null)
            {
                logger.LogWarning("No RPCs configured for chain {ChainId}", network.ChainId);
                SetState("no-rpc");
                await Task.Delay(5000, ct);
                continue;
            }

            if (!healthTracker.IsAvailable(rpc))
            {
                var backoff = healthTracker.GetBackoffUntil(rpc);
                var delay = backoff - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay < TimeSpan.FromSeconds(5) ? delay : TimeSpan.FromSeconds(5), ct);
                continue;
            }

            try
            {
                if (rpc.StartsWith("wss://", StringComparison.Ordinal))
                    await RunWebSocketAsync(rpc, ct);
                else
                    await RunHttpPollAsync(rpc, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Watcher error on chain {ChainId} rpc {Rpc}", network.ChainId, rpc);
                healthTracker.RecordFailure(rpc, options.Value.MaxRpcBackoffSeconds);
                SetState("error");
            }
        }
    }

    private void SetState(string state)
    {
        var current = statusTracker.GetAll().FirstOrDefault(s => s.ChainId == network.ChainId);
        statusTracker.Update(current is null
            ? new ChainStatus(network.ChainId, network.Name, 0, 0, 0, DateTimeOffset.MinValue, state)
            : current with { State = state });
    }

    private async Task RunWebSocketAsync(string wssUrl, CancellationToken ct)
    {
        logger.LogDebug("Chain {ChainId}: subscribing via WebSocket {Url}", network.ChainId, wssUrl);
        await foreach (var blockNumber in rpcClient.SubscribeNewHeadsAsync(wssUrl, ct))
        {
            var tag = "0x" + blockNumber.ToString("x");
            var httpRpc = rpcSelector.SelectBest(network.RpcUrls.Where(r => r.StartsWith("https://")).ToArray())
                          ?? wssUrl.Replace("wss://", "https://").Replace("ws://", "http://");

            var result = await rpcClient.GetBlockByNumberAsync(httpRpc, tag, network.ChainId, ct);
            if (result is null)
            {
                healthTracker.RecordFailure(wssUrl, options.Value.MaxRpcBackoffSeconds);
                SetState("error");
                return;
            }

            await PublishBlockResultAsync(result.Value.Block, result.Value.Transactions, wssUrl, ct);
        }
    }

    private async Task RunHttpPollAsync(string rpcUrl, CancellationToken ct)
    {
        logger.LogDebug("Chain {ChainId}: polling via HTTP {Url}", network.ChainId, rpcUrl);
        while (!ct.IsCancellationRequested && healthTracker.IsAvailable(rpcUrl))
        {
            var result = await rpcClient.GetBlockByNumberAsync(rpcUrl, "latest", network.ChainId, ct);
            if (result is null)
            {
                healthTracker.RecordFailure(rpcUrl, options.Value.MaxRpcBackoffSeconds);
                SetState("error");
                return;
            }

            var (block, txs) = result.Value;
            if (block.BlockHash != _lastSeenBlockHash)
            {
                _lastSeenBlockHash = block.BlockHash;
                await PublishBlockResultAsync(block, txs, rpcUrl, ct);
            }

            await Task.Delay(options.Value.PollIntervalMs, ct);
        }
    }

    private async Task PublishBlockResultAsync(
        RawBlock block, List<RawTransaction> transactions, string rpc, CancellationToken ct)
    {
        var blockTimeMs = _previousBlockTimestamp.HasValue
            ? (long)(block.Timestamp - _previousBlockTimestamp.Value).TotalMilliseconds
            : 0L;

        _previousBlockTimestamp = block.Timestamp;

        var finalBlock = block with { BlockTimeMs = blockTimeMs };
        await publisher.PublishBlockAsync(finalBlock, transactions, ct);
        healthTracker.RecordSuccess(rpc);

        statusTracker.Update(new ChainStatus(
            network.ChainId, network.Name,
            block.BlockNumber, transactions.Count, blockTimeMs,
            DateTimeOffset.UtcNow, "ok"));

        logger.LogDebug("Chain {ChainId} ({Name}): block {BlockNumber} txs={TxCount} blockTime={BlockTimeMs}ms",
            network.ChainId, network.Name, block.BlockNumber, transactions.Count, blockTimeMs);
    }
}
