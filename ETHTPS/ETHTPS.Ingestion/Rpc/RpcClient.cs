using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ETHTPS.Ingestion.Models;
using Microsoft.Extensions.Logging;

namespace ETHTPS.Ingestion.Rpc;

public class RpcClient(HttpClient httpClient, ILogger<RpcClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<(RawBlock Block, List<RawTransaction> Transactions)?> GetBlockByNumberAsync(
        string rpcUrl, string tag, int chainId, CancellationToken ct)
    {
        var body = $$$"""{"jsonrpc":"2.0","method":"eth_getBlockByNumber","params":["{{{tag}}}",true],"id":1}""";
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
            logger.LogDebug(ex, "GetBlockByNumber failed for {Url} tag={Tag}", rpcUrl, tag);
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
            BlockTimeMs = 0, // caller sets this
            IngestedAt = DateTimeOffset.UtcNow
        };

        return (block, transactions);
    }

    public async IAsyncEnumerable<long> SubscribeNewHeadsAsync(
        string wssUrl,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wssUrl), ct);

        var subscribeMsg = """{"jsonrpc":"2.0","method":"eth_subscribe","params":["newHeads"],"id":1}"""u8.ToArray();
        await ws.SendAsync(subscribeMsg, WebSocketMessageType.Text, true, ct);

        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var ms = new MemoryStream();
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new Memory<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) yield break;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Position = 0;
                long? blockNumber = null;
                try
                {
                    using var doc = JsonDocument.Parse(ms);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("method", out var method) &&
                        method.GetString() == "eth_subscription" &&
                        root.TryGetProperty("params", out var parms) &&
                        parms.TryGetProperty("result", out var blockHeader) &&
                        blockHeader.TryGetProperty("number", out var numProp))
                    {
                        blockNumber = ParseHexLong(numProp.GetString());
                    }
                }
                catch { /* skip malformed message */ }

                if (blockNumber.HasValue)
                    yield return blockNumber.Value;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
