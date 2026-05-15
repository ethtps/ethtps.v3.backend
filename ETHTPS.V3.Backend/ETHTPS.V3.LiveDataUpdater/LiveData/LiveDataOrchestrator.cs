using System.Collections.Concurrent;

using ETHTPS.V3.Cache.Core;
using ETHTPS.V3.Data;
using ETHTPS.V3.Data.Models;
using ETHTPS.V3.LiveDataUpdater.RPC.Models.ResponseModels;

using Microsoft.Extensions.Logging;

namespace ETHTPS.V3.LiveDataUpdater.LiveData
{
    public sealed class LiveDataOrchestrator
    {
        private readonly ILogger? _logger;

        private readonly int _maxConcurrentRequests;
        private readonly int _writeCacheEveryMs;
        private DateTime _lastCacheSet = DateTime.Now;

        private readonly IEnumerable<DataUpdater> _dataUpdaters;
        private readonly IEnumerable<EndpointMetadata> _dataUpdaterMetadata;
        private readonly RedisCacheService _redisCache;
        private readonly ConcurrentDictionary<string, BlockInfoResponseModel> _dataEntries = new();

        private readonly ETHTPSDatabase _database;

        public LiveDataOrchestrator(IEnumerable<DataUpdater> dataUpdaters, IEnumerable<EndpointMetadata> dataUpdaterMetadata, int maxConcurrentRequests, RedisCacheService redisCache, ETHTPSDatabase database, int writeCacheEveryMs = 1000, ILogger? logger = null)
        {
            _maxConcurrentRequests = maxConcurrentRequests;
            _dataUpdaters = dataUpdaters;
            _dataUpdaterMetadata = dataUpdaterMetadata;
            _redisCache = redisCache;
            _writeCacheEveryMs = writeCacheEveryMs;
            _database = database;
            _logger = logger;
        }

        private void OnUpdaterFailure(object? sender, EndpointMetadata updaterMetadata)
        {
            var updater = _dataUpdaterMetadata.First(x => x.ID == updaterMetadata.ID);
            updater.LastHit = DateTime.Now;
            updater.Enabled = false;
            updater.Healthy = false;
            updater.FailureCount++;
            updater.HitCount++;
        }

        private void OnUpdaterSuccess(object? sender, (EndpointMetadata Metadata, double TimeMs) args)
        {
            var updater = _dataUpdaterMetadata.First(x => x.ID == args.Metadata.ID);
            updater.LastHit = DateTime.Now;
            updater.AverageAccessTimeMs = ((updater.AverageAccessTimeMs * updater.HitCount) + (int)args.TimeMs) / (++updater.HitCount);
        }

        private void OnNewBlockData(object? sender, (string Provider, BlockInfoResponseModel Block) args)
        {
            _dataEntries[args.Provider] = args.Block;
            if (DateTime.Now - _lastCacheSet > TimeSpan.FromMilliseconds(_writeCacheEveryMs))
            {
                _redisCache.SetBigDictionary("AllLatestBlocks", _dataEntries);
                _lastCacheSet = DateTime.Now;
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            KeepUpdatingDatabaseWithStatusAsync(cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                var start = DateTime.Now;
                using var semaphore = new SemaphoreSlim(_maxConcurrentRequests);
                var tasks = new List<Task>();

                foreach (var updater in _dataUpdaters)
                {
                    await semaphore.WaitAsync(cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var rpcUpdater = new LiveDataRPCUpdater(_dataUpdaterMetadata.Where(y => y.Provider == updater.Provider));
                            rpcUpdater.OnSuccess += (s, e) => OnUpdaterSuccess(s, e);
                            rpcUpdater.OnFailure += (s, e) => OnUpdaterFailure(s, e);
                            rpcUpdater.OnNewBlockData += (s, e) => OnNewBlockData(s, e);

                            await rpcUpdater.UpdateLatestBlockTransactionsAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            // Optional: log exception
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken);

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                var end = DateTime.Now;
                _logger?.LogInformation($"Updated {_dataUpdaters.Count(x => x.Enabled)} updaters in {(end - start).TotalMilliseconds}ms");
                await Task.Delay(100, cancellationToken);
            }
        }

        private async void KeepUpdatingDatabaseWithStatusAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10000, token);
                await _database.BulkUpdateRPCMetadataAsync(_dataUpdaterMetadata);
            }
        }
    }
}
