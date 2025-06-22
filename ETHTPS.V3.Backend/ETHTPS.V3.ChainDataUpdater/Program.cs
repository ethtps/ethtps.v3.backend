using ETHTPS.V3.Data;

using Newtonsoft.Json;

namespace ETHTPS.V3.ChainDataUpdater
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var source = "https://chainlist.org/rpcs.json";
            var responseString = await (new HttpClient()).GetStringAsync(source);
            var data = JsonConvert.DeserializeObject<ChainInfo[]>(responseString);
            if (data == null) return;

            var database = new ETHTPSDatabase("Server=QCD\\SQLSERVER;Database=ETHTPS.Dev.Experimental;Trusted_Connection=True;Integrated Security=true;TrustServerCertificate=True;");
            foreach (var entry in data)
            {
                database.CreateProvider(entry.name, "Unknown", entry.nativeCurrency.name, entry.nativeCurrency.symbol, entry.nativeCurrency.decimals, entry.infoURL);
                Console.WriteLine($"Added {entry.name} to database");
                database.LinkRpcEndpoints(entry.name, entry.rpc.Select(x => x.url));
                Console.WriteLine($"Linked {entry.rpc.Length} RPCs to {entry.name}");
            }
        }
    }
}
