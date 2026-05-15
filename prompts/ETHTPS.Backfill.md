# ETHTPS.Backfill — Implementation System Prompt

## Context
You are implementing `ETHTPS.Backfill`, the fourth microservice in the ETHTPS backend. It crawls historical blocks for every EVM network in its configured shard range — from genesis (block 0) up to the current chain head — writing directly to TimescaleDB, bypassing Kafka. Progress is checkpointed to PostgreSQL so crawls resume correctly after restart.

## Your task
Implement `ETHTPS.Backfill` as a .NET 10 Worker Service in C#. Write production-quality code with no placeholders. Implement everything fully.

---

## Responsibilities
1. On startup, load all non-removed networks in the shard range from the `networks` table and their crawl state from `backfill_progress`. Skip chains with `status = 'completed'`.
2. Start one `ChainCrawler` per chain (bounded by `BackfillOptions.MaxConcurrentChains`).
3. Each `ChainCrawler` fetches full blocks (with transactions) sequentially from `lastBlockNumber + 1` to the chain's current head, with up to `MaxConcurrentBlocksPerChain` in-flight fetches at once.
4. For each block: compute metrics, write block + transactions + metrics directly to TimescaleDB.
5. Checkpoint progress to `backfill_progress` every `CheckpointIntervalBlocks` completed blocks.
6. Consume Kafka `network-events` to start crawls for newly added chains and cancel crawls for removed chains.

---

## Project structure
```
ETHTPS.Backfill/
  Workers/
    BackfillOrchestrator.cs       ← BackgroundService; owns ChainCrawler task pool
    NetworkEventConsumer.cs       ← BackgroundService; reacts to network-events
  Crawlers/
    ChainCrawler.cs               ← per-chain crawl loop
    BlockWindow.cs                ← result of one parallel fetch window
  Rpc/
    RpcClient.cs                  ← HTTP JSON-RPC (no WebSocket needed for backfill)
    RpcHealthTracker.cs           ← same pattern as ETHTPS.Ingestion
    RpcSelector.cs
  Repositories/
    BackfillProgressRepository.cs
    NetworkRepository.cs          ← reads networks + RPC list
    BlockWriteRepository.cs
    TransactionWriteRepository.cs ← NpgsqlBinaryImporter COPY
    MetricsWriteRepository.cs
  Processors/
    MetricsProcessor.cs           ← same pure static computation as ETHTPS.Processing
  Options/
    BackfillOptions.cs
  Infrastructure/
    SchemaInitializer.cs
  Program.cs
```

---

## Database schema

### `backfill_progress` (created by `SchemaInitializer`)
```sql
CREATE TABLE IF NOT EXISTS backfill_progress (
    chain_id           INTEGER      PRIMARY KEY,
    last_block_number  BIGINT       NOT NULL DEFAULT -1,
    target_block       BIGINT,
    status             TEXT         NOT NULL DEFAULT 'pending',
    started_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    completed_at       TIMESTAMPTZ
);
```
`status` values: `pending`, `running`, `completed`, `failed`.
`last_block_number = -1` means not yet started (will fetch from block 0).
`target_block` is set to the chain head at the time a crawl starts; updated if the crawl catches up and continues.

`SchemaInitializer` also ensures the `blocks`, `transactions`, and `metrics` tables and their hypertables exist — use the exact same DDL as in `ETHTPS.Processing`. Do not duplicate logic: copy the DDL verbatim. This service must be safe to run independently of `ETHTPS.Processing` — it cannot assume the schema already exists.

---

## BackfillOrchestrator
A `BackgroundService`:
1. On `ExecuteAsync`: calls `NetworkRepository.GetAllInShardAsync()` and `BackfillProgressRepository.GetAllAsync()`, merges them, skips `completed` chains.
2. Starts crawlers using a `SemaphoreSlim(MaxConcurrentChains)` — each crawler acquires the semaphore, runs to completion or cancellation, then releases it.
3. Holds `ConcurrentDictionary<int, CancellationTokenSource>` to support cancellation per chain.
4. `StartCrawlerAsync(Network network, BackfillProgress progress)`: creates a linked CTS, stores it, starts the `ChainCrawler.RunAsync` task inside `Task.Run`, releases semaphore when done.
5. `StopCrawler(int chainId)`: cancels the chain's CTS and removes it from the dictionary.
6. On host shutdown: cancels all active CTSes.

---

## ChainCrawler
Not a `BackgroundService` — a plain class with a `RunAsync(CancellationToken ct)` method called by `BackfillOrchestrator`.

### Startup
1. Get the chain's current head via `RpcClient.GetBlockNumberAsync()` (`eth_blockNumber`). Set `target_block` in `backfill_progress` and status to `running`.
2. Set `currentBlock = progress.LastBlockNumber + 1` (or `0` if `-1`).

### Main crawl loop
Repeat until `currentBlock > targetBlock` or `ct` is cancelled:

1. **Build a window** of up to `MaxConcurrentBlocksPerChain` block numbers: `[currentBlock, min(currentBlock + MaxConcurrentBlocksPerChain - 1, targetBlock)]`.

2. **Anchor timestamp**: Before fetching the window, retrieve the timestamp of block `currentBlock - 1`:
   - If `currentBlock == 0`: anchor = `null` (no previous block).
   - If the previous block was in the last window: use the in-memory timestamp stored from that window.
   - Otherwise: fetch from the `blocks` DB table via `BlockWriteRepository.GetTimestampAsync(chainId, currentBlock - 1)`. If not found (gap in DB), fetch from RPC.

3. **Fetch window in parallel**: use `Parallel.ForEachAsync` (or `Task.WhenAll` with a `SemaphoreSlim`) to fetch each block number in the window concurrently, each going through `RpcSelector` + `RpcHealthTracker` for RPC failover.

4. **Sort** fetched blocks by block number ascending.

5. **Compute BlockTimeMs sequentially** through the sorted list:
   - Block at index 0: `BlockTimeMs = anchor == null ? 0 : (block.Timestamp - anchor.Value).TotalMilliseconds`.
   - Block at index `i > 0`: `BlockTimeMs = (blocks[i].Timestamp - blocks[i-1].Timestamp).TotalMilliseconds`.
   - Store the last block's timestamp as the next window's in-memory anchor.

6. **Write to DB**:
   - `BlockWriteRepository.BulkInsertAsync(window blocks)`
   - `TransactionWriteRepository.BulkInsertAsync(all transactions across the window)` — use COPY
   - `MetricsWriteRepository.BulkInsertAsync(computed metrics for window)`

7. **Checkpoint**: if `completedBlocks % CheckpointIntervalBlocks == 0`, update `backfill_progress.last_block_number` and `updated_at`.

8. Advance `currentBlock` by window size.

### Completion
When `currentBlock > targetBlock`:
- If the chain has produced new blocks since the crawl started (check `eth_blockNumber` again): update `target_block` and continue the loop.
- Otherwise: update `backfill_progress` with `status = 'completed'`, `completed_at = now()`, and return.

### Error handling
- RPC fetch failure for a block: retry via `RpcSelector` up to 3 times with backoff, then skip the block and log a warning. Do not let one bad block halt the entire chain crawl.
- DB write failure: log, wait 5s, retry once. If still failing, log error and continue — do not crash the crawler.
- Never propagate exceptions out of `RunAsync` except `OperationCanceledException`.

---

## RpcClient
HTTP-only (no WebSocket needed for backfill). Implement two methods:

### `GetBlockByNumberAsync(long blockNumber, CancellationToken ct)` → `(RawBlock, List<RawTransaction>)?`
POST `eth_getBlockByNumber` with hex block number and `true`. Return null on failure. Same JSON field mapping as `ETHTPS.Ingestion`.

### `GetBlockNumberAsync(CancellationToken ct)` → `long?`
POST `eth_blockNumber`. Parse result as hex. Return null on failure.

Use a single injected `HttpClient`. Timeout per request: `BackfillOptions.RpcTimeoutMs` (default `10000` — longer than live ingestion since backfill is not latency-sensitive).

---

## Repositories

### `BackfillProgressRepository`
```csharp
Task<IReadOnlyList<BackfillProgress>> GetAllAsync(CancellationToken ct);
Task UpsertAsync(BackfillProgress progress, CancellationToken ct);         // INSERT ON CONFLICT DO UPDATE
Task UpdateCheckpointAsync(int chainId, long lastBlockNumber, CancellationToken ct);
Task MarkCompletedAsync(int chainId, CancellationToken ct);
Task MarkRunningAsync(int chainId, long targetBlock, CancellationToken ct);
```

### `NetworkRepository`
```csharp
Task<IReadOnlyList<Network>> GetAllInShardAsync(int minChainId, int maxChainId, CancellationToken ct);
```
Reads from `networks` table where `removed_at IS NULL AND chain_id BETWEEN minChainId AND maxChainId`.

### `BlockWriteRepository`
```csharp
Task BulkInsertAsync(IReadOnlyList<RawBlock> blocks, CancellationToken ct);
Task<DateTimeOffset?> GetTimestampAsync(int chainId, long blockNumber, CancellationToken ct);
```
`BulkInsertAsync` uses `INSERT ... ON CONFLICT (chain_id, block_number) DO NOTHING` for each block (or use `COPY` with a temp table + INSERT SELECT ON CONFLICT — prefer this if batch size > 50).

### `TransactionWriteRepository`
```csharp
Task BulkInsertAsync(IReadOnlyList<RawTransaction> transactions, CancellationToken ct);
```
Use `NpgsqlBinaryImporter` (COPY binary). On `PostgresException` with `SqlState == "23505"`, fall back to individual `INSERT ... ON CONFLICT DO NOTHING`.

### `MetricsWriteRepository`
```csharp
Task BulkInsertAsync(IReadOnlyList<ComputedMetrics> metrics, CancellationToken ct);
```
Same approach as `BlockWriteRepository` — `ON CONFLICT (chain_id, block_number) DO NOTHING`.

---

## MetricsProcessor
Identical to `ETHTPS.Processing`. Pure static class:
```csharp
public static ComputedMetrics Compute(RawBlock block) { ... }
```
`BlockTimeMs = 0` → `Tps = null`, `Gps = null`.

---

## NetworkEventConsumer
A `BackgroundService`:
- Consumes Kafka topic `network-events`, group id: `backfill-shard-{min}-{max}`.
- `NetworkAddedEvent` + chainId in shard range: calls `orchestrator.StartCrawlerAsync(...)` with a fresh `BackfillProgress` (status `pending`, `last_block_number = -1`).
- `NetworkRemovedEvent` + chainId in shard range: calls `orchestrator.StopCrawler(chainId)`.
- `NetworkRpcsUpdatedEvent`: ignored (crawler will naturally pick up new RPCs via `RpcSelector` on its next RPC call failure).
- Commits offsets manually after processing.

---

## Options
```csharp
public class BackfillOptions
{
    public int ShardChainIdMin { get; set; } = 1;
    public int ShardChainIdMax { get; set; } = int.MaxValue;
    public int MaxConcurrentChains { get; set; } = 50;
    public int MaxConcurrentBlocksPerChain { get; set; } = 10;
    public int CheckpointIntervalBlocks { get; set; } = 100;
    public int RpcTimeoutMs { get; set; } = 10000;
    public int MaxRpcBackoffSeconds { get; set; } = 60;
    public int TransactionBatchSize { get; set; } = 1000;
}
```

---

## Program.cs
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<BackfillOptions>(builder.Configuration.GetSection("Backfill"))
    .AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!)
    .AddHttpClient<RpcClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddSingleton<RpcHealthTracker>()
    .AddSingleton<RpcSelector>()
    .AddSingleton<BackfillProgressRepository>()
    .AddSingleton<NetworkRepository>()
    .AddSingleton<BlockWriteRepository>()
    .AddSingleton<TransactionWriteRepository>()
    .AddSingleton<MetricsWriteRepository>()
    .AddSingleton<SchemaInitializer>()
    .AddSingleton<BackfillOrchestrator>()
    .AddHostedService(sp => sp.GetRequiredService<BackfillOrchestrator>())
    .AddHostedService<NetworkEventConsumer>();

var app = builder.Build();
await app.Services.GetRequiredService<SchemaInitializer>().RunAsync();
await app.RunAsync();
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
  "Backfill": {
    "ShardChainIdMin": 1,
    "ShardChainIdMax": 250,
    "MaxConcurrentChains": 50,
    "MaxConcurrentBlocksPerChain": 10,
    "CheckpointIntervalBlocks": 100,
    "RpcTimeoutMs": 10000,
    "MaxRpcBackoffSeconds": 60,
    "TransactionBatchSize": 1000
  }
}
```

---

## NuGet packages
- `Npgsql` — PostgreSQL + binary COPY
- `Confluent.Kafka` — Kafka consumer
- `Microsoft.Extensions.Http.Resilience` — `AddStandardResilienceHandler()`
- `Microsoft.Extensions.Hosting` — Worker Service host
- `System.Text.Json` — all serialisation

---

## Constraints and rules
- Target **.NET 10**. Use modern C# throughout.
- No EF Core, no Dapper.
- All async methods accept and forward `CancellationToken`. `OperationCanceledException` propagates out of `RunAsync` cleanly.
- `ChainCrawler.RunAsync` must never throw any exception other than `OperationCanceledException` — catch and log everything else.
- `SchemaInitializer` must be fully idempotent.
- Use `NpgsqlDataSource` (singleton) and call `OpenConnectionAsync` per operation — never hold a connection open across windows.
- Log with `ILogger<T>` using structured parameters. Always include `chainId`, current block range, and progress percentage in crawler log messages.
- Progress percentage: `(currentBlock - startBlock) / (double)(targetBlock - startBlock) * 100`, logged every `CheckpointIntervalBlocks`.