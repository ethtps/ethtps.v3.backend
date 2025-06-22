using ETHTPS.V3.Data;

namespace ETHTPS.V3.Cache.Worker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting cache worker...");
            CacheBuilder cacheBuilder = new("localhost", "localhost");
            CancellationTokenSource cancellationTokenSource = new();
            Console.CancelKeyPress += (e, args) =>
            {
                Console.WriteLine("Closing worker...");
                args.Cancel = true;
                cancellationTokenSource.Cancel();
            };
            Console.WriteLine("Cache worker started");
            Console.WriteLine("Waiting for messages");
            var database = new ETHTPSDatabase("Server=QCD\\SQLSERVER;Database=ETHTPS.Dev.Experimental;Trusted_Connection=True;Integrated Security=true;TrustServerCertificate=True;");
            cacheBuilder.RegisterAction("all_providers", () =>
            {
                return database.GetAllProviders();
            });
            await cacheBuilder.RunAsync(cancellationTokenSource.Token);
        }
    }
}
