using ETHTPS.Backfill.Models;
using ETHTPS.Backfill.Options;
using ETHTPS.Backfill.Processors;
using ETHTPS.Backfill.Repositories;
using ETHTPS.Backfill.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Backfill.Crawlers;

public class ChainCrawler(
    Network network,
    BackfillProgress initialProgress,
    RpcClient rpcClient,
    RpcHealthTracker healthTracker,
    RpcSelector rpcSelector,
    BlockWriteRepository blockRepo,
    TransactionWriteRepository txRepo,
    MetricsWriteRepository metricsRepo,
    BackfillProgressRepository progressRepo,
    IOptions<BackfillOptions> options,
    ILogger<ChainCrawler> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var chainId = network.ChainId;

        var rpc = rpcSelector.SelectBest(network.RpcUrls);
        if (rpc is null)
        {
            logger.LogWarning("Chain {ChainId}: no RPCs available, skipping crawl", chainId);
            return;
        }

        var headBlock = await rpcClient.GetBlockNumberAsync(rpc, ct);
        if (!headBlock.HasValue)
        {
            logger.LogWarning("Chain {ChainId}: failed to get head block", chainId);
            return;
        }

        var targetBlock = headBlock.Value;
        await progressRepo.MarkRunningAsync(chainId, targetBlock, ct);

        var currentBlock = initialProgress.LastBlockNumber < 0 ? 0L : initialProgress.LastBlockNumber + 1;
        var startBlock = currentBlock;
        var completedBlocks = 0L;
        DateTimeOffset? inMemoryAnchorTimestamp = null;

        logger.LogInformation("Chain {ChainId}: crawling blocks {Start} to {Target}", chainId, currentBlock, targetBlock);

        while (currentBlock <= targetBlock)
        {
            ct.ThrowIfCancellationRequested();

            var windowEnd = Math.Min(currentBlock + opts.MaxConcurrentBlocksPerChain - 1, targetBlock);
            var windowSize = (int)(windowEnd - currentBlock + 1);
            var blockNumbers = Enumerable.Range(0, windowSize).Select(i => currentBlock + i).ToArray();

            // Anchor timestamp for first block in window
            DateTimeOffset? anchor = null;
            if (currentBlock == 0)
            {
                anchor = null;
            }
            else if (inMemoryAnchorTimestamp.HasValue)
            {
                anchor = inMemoryAnchorTimestamp;
            }
            else
            {
                // Try DB, then RPC
                anchor = await blockRepo.GetTimestampAsync(chainId, currentBlock - 1, ct);
                if (!anchor.HasValue)
                {
                    var anchorRpc = rpcSelector.SelectBest(network.RpcUrls);
                    if (anchorRpc is not null)
                    {
                        var anchorResult = await rpcClient.GetBlockByNumberAsync(anchorRpc, currentBlock - 1, chainId, ct);
                        anchor = anchorResult?.Block.Timestamp;
                    }
                }
            }

            // Fetch window in parallel
            var fetchSemaphore = new SemaphoreSlim(opts.MaxConcurrentBlocksPerChain);
            var fetchResults = new (RawBlock Block, List<RawTransaction> Txs)?[windowSize];

            await Task.WhenAll(blockNumbers.Select(async (blockNum, idx) =>
            {
                await fetchSemaphore.WaitAsync(ct);
                try
                {
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var fetchRpc = rpcSelector.SelectBest(network.RpcUrls);
                        if (fetchRpc is null) break;

                        if (!healthTracker.IsAvailable(fetchRpc))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), ct);
                            continue;
                        }

                        var result = await rpcClient.GetBlockByNumberAsync(fetchRpc, blockNum, chainId, ct);
                        if (result.HasValue)
                        {
                            fetchResults[idx] = result;
                            healthTracker.RecordSuccess(fetchRpc);
                            break;
                        }

                        healthTracker.RecordFailure(fetchRpc, opts.MaxRpcBackoffSeconds);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    }

                    if (!fetchResults[idx].HasValue)
                        logger.LogWarning("Chain {ChainId}: skipping block {BlockNum} after 3 failed attempts", chainId, blockNum);
                }
                finally
                {
                    fetchSemaphore.Release();
                }
            }));

            // Sort by block number and compute BlockTimeMs sequentially
            var sortedResults = fetchResults
                .Where(r => r.HasValue)
                .Select(r => r!.Value)
                .OrderBy(r => r.Block.BlockNumber)
                .ToList();

            var blocksToWrite = new List<RawBlock>(sortedResults.Count);
            var txsToWrite = new List<RawTransaction>();
            var metricsToWrite = new List<ComputedMetrics>(sortedResults.Count);

            DateTimeOffset? prevTimestamp = anchor;
            foreach (var (block, txs) in sortedResults)
            {
                var blockTimeMs = prevTimestamp.HasValue
                    ? (long)(block.Timestamp - prevTimestamp.Value).TotalMilliseconds
                    : 0L;
                prevTimestamp = block.Timestamp;

                var finalBlock = block with { BlockTimeMs = blockTimeMs };
                blocksToWrite.Add(finalBlock);
                txsToWrite.AddRange(txs.Select(tx => tx with { ChainId = chainId }));
                metricsToWrite.Add(MetricsProcessor.Compute(finalBlock));
            }

            inMemoryAnchorTimestamp = prevTimestamp;

            // Write to DB
            try
            {
                await blockRepo.BulkInsertAsync(blocksToWrite, ct);
                await txRepo.BulkInsertAsync(txsToWrite, ct);
                await metricsRepo.BulkInsertAsync(metricsToWrite, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Chain {ChainId}: DB write error for blocks {Start}-{End}, retrying once", chainId, currentBlock, windowEnd);
                await Task.Delay(5000, ct);
                try
                {
                    await blockRepo.BulkInsertAsync(blocksToWrite, ct);
                    await txRepo.BulkInsertAsync(txsToWrite, ct);
                    await metricsRepo.BulkInsertAsync(metricsToWrite, ct);
                }
                catch (Exception ex2) when (ex2 is not OperationCanceledException)
                {
                    logger.LogError(ex2, "Chain {ChainId}: DB write retry failed, continuing", chainId);
                }
            }

            completedBlocks += windowSize;
            if (completedBlocks % opts.CheckpointIntervalBlocks == 0)
            {
                await progressRepo.UpdateCheckpointAsync(chainId, windowEnd, ct);
                if (targetBlock > startBlock)
                {
                    var pct = (currentBlock - startBlock) / (double)(targetBlock - startBlock) * 100;
                    logger.LogInformation("Chain {ChainId}: progress {Pct:F1}% block {Current}/{Target}",
                        chainId, pct, currentBlock, targetBlock);
                }
            }

            currentBlock += windowSize;
        }

        // Check if chain produced new blocks
        var newHead = await rpcClient.GetBlockNumberAsync(
            rpcSelector.SelectBest(network.RpcUrls) ?? network.RpcUrls[0], ct);

        if (newHead.HasValue && newHead.Value > targetBlock)
        {
            targetBlock = newHead.Value;
            // Continue the loop — but currentBlock > old targetBlock so we need to reset
            // This is handled by updating targetBlock and continuing
            // Actually the while loop has already exited, so we restart
            logger.LogInformation("Chain {ChainId}: new blocks found, continuing from {Current} to {Target}",
                chainId, currentBlock, targetBlock);

            // Tail crawl — just run recursively with updated progress
            var updatedProgress = initialProgress with { LastBlockNumber = currentBlock - 1 };
            var tailCrawler = new ChainCrawler(network, updatedProgress, rpcClient, healthTracker,
                rpcSelector, blockRepo, txRepo, metricsRepo, progressRepo, options, logger);
            await tailCrawler.RunAsync(ct);
            return;
        }

        await progressRepo.MarkCompletedAsync(chainId, ct);
        logger.LogInformation("Chain {ChainId}: crawl completed at block {Target}", chainId, targetBlock);
    }
}
