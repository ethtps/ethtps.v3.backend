using ETHTPS.V3.Cache.Core;
using ETHTPS.V3.Data;
using ETHTPS.V3.LiveDataUpdater.LiveData;

using Microsoft.Extensions.Logging;

namespace ETHTPS.V3.LiveDataUpdater
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            ILogger logger = factory.CreateLogger("Live data updater");
            logger.LogInformation("Live data updater started");
            CancellationTokenSource tokenSource = new();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Shutting down...");
                tokenSource.Cancel();
            };
            var database = new ETHTPSDatabase("Server=QCD\\SQLSERVER;Database=ETHTPS.Dev.Experimental;Trusted_Connection=True;Integrated Security=true;TrustServerCertificate=True;");
            var updaters = database.GetAllUpdaters().Where(x => x.Enabled && x.Type == "Real-time");
            var updaterMetadata = database.GetAllEndpointMetadata().Where(x => x.Enabled);
            var redisCache = new RedisCacheService("localhost");
            LiveDataOrchestrator orchestrator = new(updaters, updaterMetadata, 200, redisCache, database, 1000, logger);
            try
            {
                await orchestrator.RunAsync(tokenSource.Token);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Exception thrown in orchestrator");
                }
            }
            logger.LogInformation("Live data updater exited");
        }
    }
}
