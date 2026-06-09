# ETHTPS.Processing — Implementation System Prompt

## Context
You are implementing `ETHTPS.Processing`, the third microservice in the ETHTPS backend. It consumes raw block and transaction data published to Kafka by `ETHTPS.Ingestion`, computes per-block TPS and GPS metrics, persists everything to TimescaleDB, updates a Redis real-time cache, and publishes computed metrics downstream to the `aggregated-metrics` Kafka topic for the API layer.

## Your task
Implement `ETHTPS.Processing` as a .NET 10 Worker Service in C#. Write production-quality code with no placeholders. Implement everything fully.

---

## Responsibilities
1. Consume `raw-blocks` from Kafka and, for each block within the configured shard range:
   - Compute instantaneous `TPS = txCount / (blockTimeMs / 1000.0)` and `GPS = gasUsed / (blockTimeMs / 1000.0)`. If `blockTimeMs == 0`, set both to `null` (first block seen for a chain; cannot compute rate).
   - Write the block row to TimescaleDB.
   - Write a metrics row to TimescaleDB.
   - Update the Redis real-time cache key for the chain.
   - Publish a `MetricsComputedEvent` to Kafka topic `aggregated-metrics`.
2. Consume `raw-transactions` from Kafka and batch-write transaction rows to TimescaleDB using PostgreSQL `COPY` for throughput.
3. Apply its own database schema on startup (idempotent).

## Sharding
Same pattern as `ETHTPS.Ingestion`. Each replica handles a configured chain ID range via `ProcessingOptions.ShardChainIdMin` and `ShardChainIdMax`. Messages outside the range are skipped (offset still committed). Kafka consumer group IDs must include the shard range: `processing-blocks-{min}-{max}` and `processing-txs-{min}-{max}`.

---

## Project structure
```
ETHTPS.Processing/
  Workers/
    BlockConsumer.cs              ← BackgroundService; consumes raw-blocks
    TransactionConsumer.cs        ← BackgroundService; consumes raw-transactions, buffers + batch flushes
  Processors/
    MetricsProcessor.cs           ← pure static computation of TPS/GPS
  Repositories/
    BlockRepository.cs            ← writes to blocks table
    TransactionRepository.cs      ← NpgsqlBinaryImporter batch COPY
    MetricsRepository.cs          ← writes to metrics table
  Cache/
    IMetricsCache.cs
    RedisMetricsCache.cs
  Publishers/
    IMetricsPublisher.cs
    KafkaMetricsPublisher.cs
  Models/
    RawBlock.cs                   ← mirrors ETHTPS.Ingestion (deserialised from Kafka)
    RawTransaction.cs
    ComputedMetrics.cs
    MetricsComputedEvent.cs
  Options/
    ProcessingOptions.cs
  Infrastructure/
    SchemaInitializer.cs          ← runs on startup; applies all DDL
  Program.cs
```

---

## Models

### `RawBlock` / `RawTransaction`
Deserialised from Kafka JSON — identical shape to what `ETHTPS.Ingestion` publishes. Define them locally; do not create a shared library.

### `ComputedMetrics`
```csharp
public record ComputedMetrics
{
    public int ChainId { get; init; }
    public long BlockNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double? Tps { get; init; }    // null when BlockTimeMs == 0
    public double? Gps { get; init; }
}
```

### `MetricsComputedEvent`
Published to Kafka topic `aggregated-metrics`, key = `chainId`:
```csharp
public record MetricsComputedEvent
{
    public int ChainId { get; init; }
    public long BlockNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double? Tps { get; init; }
    public double? Gps { get; init; }
    public DateTimeOffset ComputedAt { get; init; }
}
```

---

## Database schema
`SchemaInitializer` runs all DDL on startup using `CREATE TABLE IF NOT EXISTS` and `CREATE INDEX IF NOT EXISTS`. It must also create TimescaleDB hypertables and continuous aggregates using `IF NOT EXISTS` guards where supported, and catch + ignore the specific Postgres error for "hypertable already exists" where not.

### `blocks`
```sql
CREATE TABLE IF NOT EXISTS blocks (
    chain_id      INTEGER      NOT NULL,
    block_number  BIGINT       NOT NULL,
    block_hash    TEXT         NOT NULL,
    timestamp     TIMESTAMPTZ  NOT NULL,
    tx_count      INTEGER      NOT NULL,
    gas_used      NUMERIC      NOT NULL,
    gas_limit     NUMERIC      NOT NULL,
    block_time_ms BIGINT       NOT NULL,
    ingested_at   TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (chain_id, block_number)
);
-- Hypertable on timestamp, partitioned also by chain_id for query performance
SELECT create_hypertable('blocks', 'timestamp',
    partitioning_column => 'chain_id',
    number_partitions => 16,
    if_not_exists => TRUE);
```

### `transactions`
```sql
CREATE TABLE IF NOT EXISTS transactions (
    chain_id         INTEGER      NOT NULL,
    block_number     BIGINT       NOT NULL,
    block_hash       TEXT         NOT NULL,
    tx_hash          TEXT         NOT NULL,
    gas              NUMERIC      NOT NULL,
    block_timestamp  TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (chain_id, tx_hash)
);
SELECT create_hypertable('transactions', 'block_timestamp',
    partitioning_column => 'chain_id',
    number_partitions => 16,
    if_not_exists => TRUE);
```

### `metrics`
```sql
CREATE TABLE IF NOT EXISTS metrics (
    chain_id      INTEGER           NOT NULL,
    block_number  BIGINT            NOT NULL,
    timestamp     TIMESTAMPTZ       NOT NULL,
    tps           DOUBLE PRECISION,
    gps           DOUBLE PRECISION,
    PRIMARY KEY (chain_id, block_number)
);
SELECT create_hypertable('metrics', 'timestamp',
    partitioning_column => 'chain_id',
    number_partitions => 16,
    if_not_exists => TRUE);
```

### Continuous aggregates (also created in `SchemaInitializer`)
Create one materialized view per rollup interval. Pattern shown for 1 minute; repeat for `5 minutes`, `1 hour`, `1 day`:
```sql
CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_1m
WITH (timescaledb.continuous) AS
SELECT
    chain_id,
    time_bucket('1 minute', timestamp) AS bucket,
    avg(tps)  AS avg_tps,
    max(tps)  AS max_tps,
    avg(gps)  AS avg_gps,
    max(gps)  AS max_gps,
    count(*)  AS block_count
FROM metrics
WHERE tps IS NOT NULL
GROUP BY chain_id, bucket
WITH NO DATA;

CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_5m  WITH (timescaledb.continuous) AS ... (same, bucket '5 minutes')  WITH NO DATA;
CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_1h  WITH (timescaledb.continuous) AS ... (same, bucket '1 hour')     WITH NO DATA;
CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_1d  WITH (timescaledb.continuous) AS ... (same, bucket '1 day')      WITH NO DATA;
```

Add refresh policies for each (refresh lag = 1x bucket width, start offset = 2x bucket width):
```sql
SELECT add_continuous_aggregate_policy('metrics_1m',
    start_offset => INTERVAL '2 minutes',
    end_offset   => INTERVAL '1 minute',
    schedule_interval => INTERVAL '1 minute',
    if_not_exists => TRUE);
```
Repeat for the other three views with matching intervals.

---

## MetricsProcessor
Pure static class — no dependencies:
```csharp
public static class MetricsProcessor
{
    public static ComputedMetrics Compute(RawBlock block)
    {
        double? tps = null, gps = null;
        if (block.BlockTimeMs > 0)
        {
            var seconds = block.BlockTimeMs / 1000.0;
            tps = block.TransactionCount / seconds;
            gps = (double)block.GasUsed / seconds;
        }
        return new ComputedMetrics
        {
            ChainId = block.ChainId,
            BlockNumber = block.BlockNumber,
            Timestamp = block.Timestamp,
            Tps = tps,
            Gps = gps
        };
    }
}
```

---

## BlockConsumer
A `BackgroundService` that:
1. Subscribes to Kafka topic `raw-blocks` with group id `processing-blocks-{min}-{max}`.
2. For each message:
   - Deserialises to `RawBlock`.
   - Skips if `chainId` outside shard range (commits offset).
   - Calls `MetricsProcessor.Compute(block)`.
   - Calls `BlockRepository.InsertAsync(block)` — use `INSERT ... ON CONFLICT DO NOTHING` (idempotent).
   - Calls `MetricsRepository.InsertAsync(metrics)` — same conflict strategy.
   - Calls `RedisMetricsCache.SetAsync(metrics)`.
   - Calls `KafkaMetricsPublisher.PublishAsync(event)`.
   - Commits Kafka offset manually.
3. Log each processed block at `Debug` level including `chainId`, `blockNumber`, `tps`, `gps`.

---

## TransactionConsumer
A `BackgroundService` that:
1. Subscribes to Kafka topic `raw-transactions` with group id `processing-txs-{min}-{max}`.
2. Reads messages and writes them to a `Channel<RawTransaction>` (bounded capacity: `ProcessingOptions.TransactionChannelCapacity`, default `50_000`). If the channel is full, apply backpressure by awaiting `WriteAsync`.
3. A separate flush loop (`Task`, started in `StartAsync`):
   - Reads up to `ProcessingOptions.TransactionBatchSize` (default `1000`) items from the channel, or waits up to `ProcessingOptions.TransactionFlushIntervalMs` (default `500`) ms, whichever comes first.
   - Calls `TransactionRepository.BulkInsertAsync(batch)`.
   - Commits Kafka offsets for all messages in the batch.
4. Skips any transaction whose `chainId` is outside the shard range.

---

## Repositories

### `BlockRepository`
Single method: `InsertAsync(RawBlock block, CancellationToken ct)`.
Use `INSERT INTO blocks (...) VALUES (...) ON CONFLICT (chain_id, block_number) DO NOTHING`.
Use raw Npgsql with `NpgsqlDataSource`.

### `MetricsRepository`
Single method: `InsertAsync(ComputedMetrics metrics, CancellationToken ct)`.
Same conflict strategy: `ON CONFLICT (chain_id, block_number) DO NOTHING`.

### `TransactionRepository`
Single method: `BulkInsertAsync(IReadOnlyList<RawTransaction> transactions, CancellationToken ct)`.
Use `NpgsqlBinaryImporter` (COPY binary protocol) for maximum throughput:
```csharp
await using var writer = await conn.BeginBinaryImportAsync(
    "COPY transactions (chain_id, block_number, block_hash, tx_hash, gas, block_timestamp) FROM STDIN (FORMAT BINARY)", ct);
foreach (var tx in transactions)
{
    await writer.StartRowAsync(ct);
    await writer.WriteAsync(tx.ChainId, ct);
    // ... write all fields
}
await writer.CompleteAsync(ct);
```
Handle `PostgresException` with `SqlState == "23505"` (unique violation) by falling back to individual `INSERT ... ON CONFLICT DO NOTHING` for the batch — this is a rare case during backfill overlap.

---

## RedisMetricsCache
```csharp
public interface IMetricsCache
{
    Task SetAsync(ComputedMetrics metrics, CancellationToken ct);
}
```
Implementation uses `StackExchange.Redis`. Key pattern: `ethtps:live:{chainId}`. Value: JSON-serialised object `{ tps, gps, blockNumber, timestamp }`. Set TTL to `ProcessingOptions.RedisLiveTtlSeconds` (default `60`) on every write — stale entries auto-expire if a chain stops producing blocks.

---

## KafkaMetricsPublisher
```csharp
public interface IMetricsPublisher
{
    Task PublishAsync(MetricsComputedEvent evt, CancellationToken ct);
}
```
Publishes to topic `aggregated-metrics`, key = `chainId.ToString()`. Serialise with `System.Text.Json`.

---

## Options
```csharp
public class ProcessingOptions
{
    public int ShardChainIdMin { get; set; } = 1;
    public int ShardChainIdMax { get; set; } = int.MaxValue;
    public int TransactionBatchSize { get; set; } = 1000;
    public int TransactionFlushIntervalMs { get; set; } = 500;
    public int TransactionChannelCapacity { get; set; } = 50_000;
    public int RedisLiveTtlSeconds { get; set; } = 60;
}
```

---

## Program.cs
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<ProcessingOptions>(builder.Configuration.GetSection("Processing"))
    .AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!)
    .AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!))
    .AddSingleton<IMetricsCache, RedisMetricsCache>()
    .AddSingleton<IMetricsPublisher, KafkaMetricsPublisher>()
    .AddSingleton<BlockRepository>()
    .AddSingleton<MetricsRepository>()
    .AddSingleton<TransactionRepository>()
    .AddSingleton<SchemaInitializer>()
    .AddHostedService<BlockConsumer>()
    .AddHostedService<TransactionConsumer>();

var app = builder.Build();
await app.Services.GetRequiredService<SchemaInitializer>().RunAsync();
await app.RunAsync();
```

---

## appsettings.json shape
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ethtps;Username=ethtps;Password=ethtps",
    "Redis": "localhost:6379"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Processing": {
    "ShardChainIdMin": 1,
    "ShardChainIdMax": 250,
    "TransactionBatchSize": 1000,
    "TransactionFlushIntervalMs": 500,
    "TransactionChannelCapacity": 50000,
    "RedisLiveTtlSeconds": 60
  }
}
```

---

## NuGet packages to use
- `Npgsql` — PostgreSQL + binary COPY
- `Confluent.Kafka` — Kafka consumer + producer
- `StackExchange.Redis` — Redis client
- `Microsoft.Extensions.Hosting` — Worker Service host
- `System.Text.Json` — all serialisation

---

## Constraints and rules
- Target **.NET 10**. Use modern C# features throughout.
- No EF Core, no Dapper, no ORM of any kind.
- All async methods accept and forward `CancellationToken`. Never swallow `OperationCanceledException`.
- `SchemaInitializer` must be fully idempotent — safe to run against an already-initialised database.
- `TransactionRepository.BulkInsertAsync` must handle the unique-violation fallback as described.
- Do not create `HttpClient` or `NpgsqlConnection` inside loops — use injected `NpgsqlDataSource` and call `OpenConnectionAsync` per operation unit.
- Log with `ILogger<T>` using structured parameters. Always include `chainId` and `blockNumber` in log messages where applicable.
- `BlockConsumer` and `TransactionConsumer` must commit Kafka offsets only after successful DB writes — at-least-once delivery, idempotent inserts handle duplicates.