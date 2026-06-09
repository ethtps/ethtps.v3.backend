using System.Text;
using System.Text.Json;
using ETHTPS.Backfill.Models;
using Microsoft.Extensions.Logging;

namespace ETHTPS.Backfill.Rpc;

public class RpcClient(HttpClient httpClient, ILogger<RpcClient> logger)
{
    public async Task<(RawBlock Block, List<RawTransaction> Transactions)?> GetBlockByNumberAsync(
        string rpcUrl, long blockNumber, int chainId, CancellationToken ct)
    {
        var hex = "0x" + blockNumber.ToString("x");
        var body = $$$"""{"jsonrpc":"2.0","method":"eth_getBlockByNumber","params":["{{{hex}}}",true],"id":1}""";
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(rpcUrl, content, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
                return null;

            return ParseBlock(result, chainId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GetBlockByNumber {BlockNumber} failed for {Url}", blockNumber, rpcUrl);
            return null;
        }
    }

    public async Task<long?> GetBlockNumberAsync(string rpcUrl, CancellationToken ct)
    {
        const string body = """{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}""";
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(rpcUrl, content, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("result", out var result)) return null;
            return ParseHexLong(result.GetString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "eth_blockNumber failed for {Url}", rpcUrl);
            return null;
        }
    }

    private static (RawBlock, List<RawTransaction>) ParseBlock(JsonElement result, int chainId)
    {
        var hash = result.GetProperty("hash").GetString() ?? "";
        var number = ParseHexLong(result.GetProperty("number").GetString());
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(ParseHexLong(result.GetProperty("timestamp").GetString()));
        var gasUsed = ParseHexULong(result.GetProperty("gasUsed").GetString());
        var gasLimit = ParseHexULong(result.GetProperty("gasLimit").GetString());
        var txArray = result.GetProperty("transactions");
        var transactions = new List<RawTransaction>();

        foreach (var tx in txArray.EnumerateArray())
        {
            transactions.Add(new RawTransaction
            {
                ChainId = chainId,
                BlockHash = hash,
                BlockNumber = number,
                TxHash = tx.GetProperty("hash").GetString() ?? "",
                Gas = ParseHexULong(tx.GetProperty("gas").GetString()),
                BlockTimestamp = timestamp
            });
        }

        var block = new RawBlock
        {
            ChainId = chainId,
            BlockHash = hash,
            BlockNumber = number,
            Timestamp = timestamp,
            TransactionCount = transactions.Count,
            GasUsed = gasUsed,
            GasLimit = gasLimit,
            BlockTimeMs = 0,
            IngestedAt = DateTimeOffset.UtcNow
        };

        return (block, transactions);
    }

    private static long ParseHexLong(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return 0;
        var s = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        return string.IsNullOrEmpty(s) ? 0 : Convert.ToInt64(s, 16);
    }

    private static ulong ParseHexULong(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return 0;
        var s = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        return string.IsNullOrEmpty(s) ? 0 : Convert.ToUInt64(s, 16);
    }
}
