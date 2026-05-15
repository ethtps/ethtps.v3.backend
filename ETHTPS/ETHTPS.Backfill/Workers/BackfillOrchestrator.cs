using System.Collections.Concurrent;
using ETHTPS.Backfill.Crawlers;
using ETHTPS.Backfill.Models;
using ETHTPS.Backfill.Options;
using ETHTPS.Backfill.Repositories;
using ETHTPS.Backfill.Rpc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Backfill.Workers;

public class BackfillOrchestrator(
    NetworkRepository networkRepository,
    BackfillProgressRepository progressRepository,
    RpcClient rpcClient,
    RpcHealthTracker healthTracker,
    RpcSelector rpcSelector,
    BlockWriteRepository blockRepo,
    TransactionWriteRepository txRepo,
    MetricsWriteRepository metricsRepo,
    IOptions<BackfillOptions> options,
    ILogger<BackfillOrchestrator> logger,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _crawlerCts = new();
    private CancellationToken _hostToken;
    private SemaphoreSlim? _semaphore;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostToken = stoppingToken;
        _semaphore = new SemaphoreSlim(options.Value.MaxConcurrentChains);

        var opts = options.Value;
        var networks = await networkRepository.GetAllInShardAsync(opts.ShardChainIdMin, opts.ShardChainIdMax, stoppingToken);
        var progressList = await progressRepository.GetAllAsync(stoppingToken);
        var progressMap = progressList.ToDictionary(p => p.ChainId);

        var tasks = new List<Task>();
        foreach (var network in networks)
        {
            var progress = progressMap.TryGetValue(network.ChainId, out var p) ? p : new BackfillProgress { ChainId = network.ChainId };
            if (progress.Status == "completed") continue;

            var task = StartCrawlerAsync(network, progress, stoppingToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    public async Task StartCrawlerAsync(Network network, BackfillProgress progress, CancellationToken ct)
    {
        await _semaphore!.WaitAsync(ct);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _crawlerCts[network.ChainId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                // Upsert initial progress record
                await progressRepository.UpsertAsync(progress, ct);

                var crawler = new ChainCrawler(network, progress, rpcClient, healthTracker, rpcSelector,
                    blockRepo, txRepo, metricsRepo, progressRepository, options,
                    loggerFactory.CreateLogger<ChainCrawler>());

                await crawler.RunAsync(cts.Token);
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                logger.LogError(ex, "ChainCrawler for chain {ChainId} threw unexpectedly", network.ChainId);
            }
            finally
            {
                _crawlerCts.TryRemove(network.ChainId, out _);
                _semaphore.Release();
            }
        }, ct);
    }

    public void StopCrawler(int chainId)
    {
        if (_crawlerCts.TryRemove(chainId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            logger.LogInformation("Cancelled crawl for chain {ChainId}", chainId);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (_, cts) in _crawlerCts)
        {
            cts.Cancel();
            cts.Dispose();
        }
        return base.StopAsync(cancellationToken);
    }
}
