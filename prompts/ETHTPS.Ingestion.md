# ETHTPS.Ingestion — Implementation System Prompt

## Context
You are implementing `ETHTPS.Ingestion`, the second microservice in the ETHTPS backend. ETHTPS monitors Ethereum network throughput (TPS and GPS) across 500+ EVM networks. This service is responsible for maintaining a live pool of per-chain block watchers, fetching full blocks as they are produced, and publishing raw block and transaction data to Kafka for downstream processing.

## Your task
Implement `ETHTPS.Ingestion` as a .NET 10 Worker Service in C#. Write production-quality code with no placeholders. Implement everything fully.

---

## Responsibilities
1. On startup, read all non-removed networks within this replica's configured chain ID shard range from PostgreSQL and start one `NetworkWatcher` per chain.
2. Consume the Kafka topic `network-events` (published by `ETHTPS.ChainRegistry`) and react: start a watcher for added chains, stop for removed chains, restart for RPC-updated chains — only if the chain ID falls within this replica's shard range.
3. Each `NetworkWatcher` fetches full blocks (including all transactions) as they are produced and publishes them to Kafka topics `raw-blocks` and `raw-transactions`.
4. Manage RPC health per chain: track failure counts, apply exponential backoff, and automatically failover to the next available RPC.

---

## Sharding
Each replica is configured with a chain ID range via `IngestionOptions`:
```json
{
  "Ingestion": {
    "ShardChainIdMin": 1,
    "ShardChainIdMax": 250
  }
}
```
A network event or DB-loaded chain is only handled by this replica if `ShardChainIdMin <= chainId <= ShardChainIdMax`. All other chain IDs are silently ignored.

---

## Project structure
```
ETHTPS.Ingestion/
  Workers/
    IngestionOrchestrator.cs      ← BackgroundService; owns watcher pool lifecycle
    NetworkEventConsumer.cs       ← BackgroundService; Kafka consumer for network-events
  Watchers/
    NetworkWatcher.cs             ← per-chain async watcher loop
    WatcherHandle.cs              ← running Task + CancellationTokenSource pair
  Rpc/
    RpcClient.cs                  ← JSON-RPC calls over HTTP and WebSocket
    RpcHealthTracker.cs           ← per-RPC health scores and backoff state
    RpcSelector.cs                ← picks the best healthy RPC for a chain
  Publishers/
    IBlockPublisher.cs
    KafkaBlockPublisher.cs
  Models/
    RawBlock.cs
    RawTransaction.cs
  Options/
    IngestionOptions.cs
  Program.cs
```

---

## Models

### `RawBlock`
Published to Kafka topic `raw-blocks`, key = `chainId`:
```csharp
public record RawBlock
{
    public int ChainId { get; init; }
    public string BlockHash { get; init; } = "";
    public long BlockNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }       // block timestamp from chain
    public int TransactionCount { get; init; }
    public ulong GasUsed { get; init; }                  // block-level gasUsed
    public ulong GasLimit { get; init; }
    public long BlockTimeMs { get; init; }               // milliseconds since previous block timestamp; 0 if unknown
    public DateTimeOffset IngestedAt { get; init; }      // wall clock time this replica saw the block
}
```

### `RawTransaction`
Published to Kafka topic `raw-transactions`, key = `chainId`:
```csharp
public record RawTransaction
{
    public int ChainId { get; init; }
    public string BlockHash { get; init; } = "";
    public long BlockNumber { get; init; }
    public string TxHash { get; init; } = "";
    public ulong Gas { get; init; }                      // gas limit declared by tx (from eth_getBlockByNumber)
    public DateTimeOffset BlockTimestamp { get; init; }
}
```

> Note: `eth_getBlockByNumber` with `true` provides per-transaction `gas` (limit), not `gasUsed` (which requires receipts). Actual gas consumed per transaction is not available here. Block-level `gasUsed` (available in the block header) is sufficient for GPS calculation and is carried in `RawBlock`.

---

## NetworkWatcher — per-chain watcher loop

Each watcher is a long-running `Task` (not a `BackgroundService`) managed by `IngestionOrchestrator`. It receives the chain's network info (chainId, ordered RPC list) and a `CancellationToken`.

### RPC strategy
1. Iterate the RPC list in order (WebSocket `wss://` URLs come first — `RpcSelector` enforces this).
2. **WebSocket RPCs**: open a `ClientWebSocket`, send `eth_subscribe newHeads`. On each notification, fetch the full block via `eth_getBlockByNumber(blockNumber, true)` over the same or a fresh HTTP connection.
3. **HTTP-only RPCs**: poll `eth_getBlockByNumber("latest", true)` every `IngestionOptions.PollIntervalMs` (default: `2000`). Track last seen block hash; skip publish if block hash matches the previous one.
4. On any RPC error (connection failure, timeout, malformed response, WebSocket close): record failure in `RpcHealthTracker`, select the next best RPC via `RpcSelector`, and reconnect. Apply exponential backoff per RPC starting at 1s, doubling up to `IngestionOptions.MaxRpcBackoffSeconds` (default: `60`).
5. If all RPCs for a chain are in backoff simultaneously, wait 5 seconds then retry the least-recently-failed one.
6. Never throw out of the watcher loop — catch all exceptions, log them, and continue the retry cycle. Only exit cleanly when `CancellationToken` is cancelled.

### Block publish flow
For each new block received:
1. Parse the JSON-RPC response into `RawBlock` + a list of `RawTransaction`.
2. Compute `BlockTimeMs` as the difference in milliseconds between this block's timestamp and the previous block's timestamp. On the first block seen for a chain (no previous known), set `BlockTimeMs = 0`.
3. Publish `RawBlock` to `raw-blocks`.
4. Publish each `RawTransaction` to `raw-transactions`.
5. Record success in `RpcHealthTracker` for the RPC that served this block.

---

## RpcHealthTracker
Tracks per-RPC-URL state:
- `FailureCount`: incremented on each failure for this URL.
- `BackoffUntil`: `DateTimeOffset`; the URL is considered unavailable until this time passes.
- `RecordSuccess(string url)`: resets `FailureCount` to 0 and clears `BackoffUntil`.
- `RecordFailure(string url, int maxBackoffSeconds)`: increments `FailureCount` and sets `BackoffUntil = now + min(2^FailureCount, maxBackoffSeconds) seconds`.
- `IsAvailable(string url)`: returns `true` if `BackoffUntil < now`.
- Thread-safe: use `ConcurrentDictionary<string, RpcUrlState>`.

## RpcSelector
- Given a chain's ordered RPC list and `RpcHealthTracker`, returns the first available URL (i.e. `IsAvailable == true`).
- If no URL is available, returns the one with the earliest `BackoffUntil` (caller waits and retries).
- WebSocket URLs (`wss://`) are always sorted before HTTP URLs (`https://`) within the same availability tier.

---

## RpcClient
A lightweight JSON-RPC client. Do not use any third-party Ethereum library. Implement the two calls needed directly:

### `eth_getBlockByNumber(tag, true)` over HTTP
- `tag` is either a hex block number (`"0x..."`) or `"latest"`.
- POST to the RPC URL with body:
  ```json
  {"jsonrpc":"2.0","method":"eth_getBlockByNumber","params":["<tag>",true],"id":1}
  ```
- Parse the `result` object into `RawBlock` + `List<RawTransaction>`. Relevant JSON fields:
  - Block: `hash`, `number` (hex), `timestamp` (hex, Unix seconds), `transactions` (array), `gasUsed` (hex), `gasLimit` (hex)
  - Transaction: `hash`, `gas` (hex)
- Use `System.Net.Http.HttpClient` (singleton, injected). Set a request timeout of `IngestionOptions.RpcTimeoutMs` (default: `5000`).

### `eth_subscribe newHeads` over WebSocket
- Send subscription request, then loop receiving messages.
- Each notification has shape: `{"jsonrpc":"2.0","method":"eth_subscription","params":{"result":{<block header>},"subscription":"<id>"}}`
- Extract `number` (hex block number) from the notification and hand it to the caller; the caller then calls `eth_getBlockByNumber` for the full block.
- Use `System.Net.WebSockets.ClientWebSocket`. Read in a loop using a reusable `ArrayPool<byte>` buffer.
- Surface errors (WebSocket close, receive exception) by returning from the subscription loop; the watcher handles reconnection.

All hex-to-number parsing must handle the `0x` prefix. Use `Convert.ToUInt64(hex, 16)` or equivalent.

---

## IngestionOrchestrator
A `BackgroundService`:
- On `StartAsync`: load all networks from DB where `removed_at IS NULL AND chain_id BETWEEN ShardChainIdMin AND ShardChainIdMax`. Start one watcher per network.
- Exposes `StartWatcher(Network network)`, `StopWatcher(int chainId)`, `RestartWatcher(Network network)` — called by `NetworkEventConsumer`.
- Holds `ConcurrentDictionary<int, WatcherHandle>` (chainId → handle).
- `StartWatcher`: creates a linked `CancellationTokenSource` from the host `CancellationToken`, starts the watcher `Task`, stores the handle.
- `StopWatcher`: cancels the handle's CTS, awaits the task (with a 5s timeout), removes from dictionary.
- On host shutdown (`StopAsync`): cancel all running watchers and await them.

## NetworkEventConsumer
A `BackgroundService`:
- Consumes Kafka topic `network-events` (group id: `ingestion-shard-{ShardChainIdMin}-{ShardChainIdMax}`).
- Deserialises events using `System.Text.Json`; routes on the `"type"` discriminator field:
  - `NetworkAddedEvent` + chainId in shard range → `orchestrator.StartWatcher(network)`
  - `NetworkRemovedEvent` + chainId in shard range → `orchestrator.StopWatcher(chainId)`
  - `NetworkRpcsUpdatedEvent` + chainId in shard range → `orchestrator.RestartWatcher(network)`
- Commits Kafka offsets manually after successful processing.

---

## IBlockPublisher / KafkaBlockPublisher
```csharp
public interface IBlockPublisher
{
    Task PublishBlockAsync(RawBlock block, IReadOnlyList<RawTransaction> transactions, CancellationToken ct);
}
```
- `KafkaBlockPublisher` publishes `RawBlock` to topic `raw-blocks` and each `RawTransaction` to `raw-transactions`.
- Key for both: `chainId.ToString()`.
- Serialize with `System.Text.Json`.
- Use a single `IProducer<string, string>` singleton (Confluent.Kafka).

---

## Options
```csharp
public class IngestionOptions
{
    public int ShardChainIdMin { get; set; } = 1;
    public int ShardChainIdMax { get; set; } = int.MaxValue;
    public int PollIntervalMs { get; set; } = 2000;
    public int RpcTimeoutMs { get; set; } = 5000;
    public int MaxRpcBackoffSeconds { get; set; } = 60;
}
```

---

## Program.cs
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"))
    .AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!)
    .AddHttpClient<RpcClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddSingleton<RpcHealthTracker>()
    .AddSingleton<RpcSelector>()
    .AddSingleton<IBlockPublisher, KafkaBlockPublisher>()
    .AddSingleton<IngestionOrchestrator>()
    .AddHostedService(sp => sp.GetRequiredService<IngestionOrchestrator>())
    .AddHostedService<NetworkEventConsumer>();

builder.Build().Run();
```

---

## appsettings.json shape
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ethtps;Username=ethtps;Password=ethtps"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Ingestion": {
    "ShardChainIdMin": 1,
    "ShardChainIdMax": 250,
    "PollIntervalMs": 2000,
    "RpcTimeoutMs": 5000,
    "MaxRpcBackoffSeconds": 60
  }
}
```

---

## NuGet packages to use
- `Npgsql` — PostgreSQL (read networks on startup)
- `Confluent.Kafka` — Kafka consumer + producer
- `Microsoft.Extensions.Http.Resilience` — `AddStandardResilienceHandler()`
- `Microsoft.Extensions.Hosting` — Worker Service host
- `System.Text.Json` — all serialization and JSON-RPC parsing

Do not use Nethereum or any other Ethereum library. Implement JSON-RPC calls directly as specified.

---

## Constraints and rules
- Target **.NET 10**. Use modern C# features throughout (primary constructors, collection expressions, `allows ref struct`, etc.).
- No EF Core. No Dapper. Raw Npgsql for any DB access.
- All async methods accept and forward `CancellationToken`. Never swallow `OperationCanceledException`.
- Watcher loops must never propagate unhandled exceptions — catch, log, and retry.
- `HttpClient` instances must not be created inside loops — inject a singleton or use `IHttpClientFactory`.
- Use `ArrayPool<byte>.Shared` for WebSocket receive buffers — do not allocate per-message.
- Log with `ILogger<T>` using structured parameters. Include `chainId` in every watcher log message.
- Use `ConcurrentDictionary` for all shared state accessed from multiple watcher tasks.
- Do not use `Task.Run` to wrap sync work — all IO must be genuinely async.