# ETHTPS.ChainRegistry — Implementation System Prompt

## Context
You are implementing `ETHTPS.ChainRegistry`, the first microservice in the ETHTPS backend — a system that monitors Ethereum network throughput (TPS and GPS) across Ethereum mainnet and all L2s (~500+ networks). This service is the source of truth for which networks exist and which RPC endpoints are available for each.

## Your task
Implement the `ETHTPS.ChainRegistry` .NET 8 Worker Service in C# from scratch. Write production-quality code. Do not add placeholder comments like "implement this later" — implement everything fully.

---

## Responsibilities
1. Fetch the full list of EVM networks from `https://chainid.network/chains.json` on startup and then every hour.
2. Diff the fetched list against the database to detect added, removed, and RPC-updated networks.
3. Persist all changes to PostgreSQL.
4. Publish Kafka events for each change so downstream services (the watcher pool) can react in real time.
5. On chainlist fetch failure, log a warning and skip the sync cycle — retain the last known DB state (stale-ok).

---

## Project structure
```
ETHTPS.ChainRegistry/
  Workers/
    ChainRegistryWorker.cs        ← main BackgroundService sync loop
  Clients/
    ChainlistClient.cs            ← HTTP fetch + RPC filtering
    ChainlistNetwork.cs           ← DTO matching chains.json shape
  Repositories/
    INetworkRepository.cs
    NetworkRepository.cs          ← raw Npgsql (no ORM)
  Models/
    Network.cs                    ← DB entity / domain model
  Events/
    NetworkEvents.cs              ← NetworkAddedEvent, NetworkRemovedEvent, NetworkRpcsUpdatedEvent
  Options/
    ChainRegistryOptions.cs
  Program.cs
```

---

## Models

### `Network` (DB entity)
```csharp
public record Network
{
    public int ChainId { get; init; }
    public string Name { get; init; } = "";
    public string[] RpcUrls { get; init; } = [];
    public bool Enabled { get; init; } = true;
    public DateTimeOffset? RemovedAt { get; init; }
    public DateTimeOffset LastSyncedAt { get; init; }
}
```

### `ChainlistNetwork` (API DTO)
Map from the JSON shape of `https://chainid.network/chains.json`. Each object has at minimum: `chainId` (int), `name` (string), `rpc` (string[]). Ignore all other fields.

### Kafka events
All events inherit from `NetworkEvent(int ChainId, DateTimeOffset OccurredAt)`:
- `NetworkAddedEvent(int ChainId, string Name, string[] RpcUrls, DateTimeOffset OccurredAt)`
- `NetworkRemovedEvent(int ChainId, DateTimeOffset OccurredAt)`
- `NetworkRpcsUpdatedEvent(int ChainId, string[] NewRpcUrls, DateTimeOffset OccurredAt)`

---

## ChainlistClient
- Fetches `https://chainid.network/chains.json` via `HttpClient`.
- Returns `null` on any failure (do not throw) — the worker treats null as "skip this cycle".
- Contains a `static string[] FilterUsableRpcs(string[] rpcs)` method that keeps only RPC URLs that:
  - Start with `https://` or `wss://`
  - Do not contain `${` (templated API keys, e.g. `${INFURA_API_KEY}`)
  - Do not contain the substrings `API_KEY` or `api-key` (case-sensitive check for both)
  - Are distinct (deduplicated)
- Prefer `wss://` URLs first in the returned array (sort them before `https://`), as the watcher pool will prefer WebSocket subscriptions.

---

## ChainRegistryWorker
A `BackgroundService` that:
1. Runs `SyncAsync` immediately on startup, then waits `ChainRegistryOptions.SyncInterval` (default 1 hour) before each subsequent sync.
2. In `SyncAsync`:
   - Fetches from chainlist. If null, logs a warning and returns early.
   - Loads all current networks from the DB.
   - Computes three sets: **added** (in chainlist, not in DB), **removed** (in DB, not in chainlist), **updated** (in both, but filtered RPC list has changed).
   - For each added network: upserts to DB, publishes `NetworkAddedEvent` to Kafka.
   - For each removed network: sets `removed_at = now` in DB (soft delete), publishes `NetworkRemovedEvent`.
   - For each updated network: upserts new RPC list to DB, publishes `NetworkRpcsUpdatedEvent`.
   - Logs a summary line: `Sync complete — added={A} removed={R} updated={U}`.
3. Uses `INetworkRepository` and `IKafkaProducer` injected via constructor.

---

## INetworkRepository / NetworkRepository
Interface:
```csharp
public interface INetworkRepository
{
    Task<IReadOnlyList<Network>> GetAllAsync(CancellationToken ct);
    Task UpsertAsync(Network network, CancellationToken ct);
    Task MarkRemovedAsync(int chainId, CancellationToken ct);
}
```
Implementation uses **raw Npgsql** (no EF Core, no Dapper). Use `NpgsqlDataSource` injected as a singleton. `GetAllAsync` fetches all rows regardless of `enabled` or `removed_at` — filtering is the caller's concern. `UpsertAsync` uses `INSERT ... ON CONFLICT (chain_id) DO UPDATE`.

---

## Kafka
Wrap Confluent.Kafka. The `IKafkaProducer` interface:
```csharp
public interface IKafkaProducer
{
    Task PublishAsync<T>(string topic, string key, T value, CancellationToken ct);
}
```
- All network events go to the topic `network-events`.
- Use `chainId.ToString()` as the Kafka message key so all events for a chain land on the same partition.
- Serialize event values as JSON using `System.Text.Json`. Include a `"type"` discriminator field in the serialized JSON matching the event class name (e.g. `"NetworkAddedEvent"`).
- Implement `IKafkaProducer` as a singleton that owns a single `IProducer<string, string>`.

---

## PostgreSQL schema
The service should apply this schema on startup (run once, idempotent):
```sql
CREATE TABLE IF NOT EXISTS networks (
    chain_id       INTEGER PRIMARY KEY,
    name           TEXT        NOT NULL,
    rpc_urls       TEXT[]      NOT NULL,
    enabled        BOOLEAN     NOT NULL DEFAULT TRUE,
    removed_at     TIMESTAMPTZ,
    last_synced_at TIMESTAMPTZ NOT NULL
);
```

---

## Options
```csharp
public class ChainRegistryOptions
{
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(1);
    public string ChainlistUrl { get; set; } = "https://chainid.network/chains.json";
}
```
Bind from `appsettings.json` section `"ChainRegistry"`.

---

## Program.cs
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<ChainRegistryOptions>(builder.Configuration.GetSection("ChainRegistry"))
    .AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!)
    .AddHttpClient<ChainlistClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddSingleton<INetworkRepository, NetworkRepository>()
    .AddSingleton<IKafkaProducer, KafkaProducer>()
    .AddHostedService<ChainRegistryWorker>();

builder.Build().Run();
```

---

## NuGet packages to use
- `Npgsql` — PostgreSQL driver
- `Confluent.Kafka` — Kafka producer
- `Microsoft.Extensions.Http.Resilience` — `AddStandardResilienceHandler()` (Polly)
- `Microsoft.Extensions.Hosting` — Worker Service host
- `System.Text.Json` — already in-box, use for all serialization

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
  "ChainRegistry": {
    "SyncInterval": "01:00:00"
  }
}
```

---

## Constraints and rules
- Target **.NET 10**.
- Use **C# 12** features where appropriate (primary constructors, collection expressions, etc.).
- No EF Core. No Dapper. Raw Npgsql only for DB access.
- No MediatR or other mediator libraries.
- All async methods must accept and pass through `CancellationToken`.
- Do not catch `OperationCanceledException` — let it propagate to the host for clean shutdown.
- Do not use `Thread.Sleep`. Use `Task.Delay` with the cancellation token.
- `ChainlistClient` must never throw — always return null on failure.
- Log using `ILogger<T>` with structured logging (named parameters, not string interpolation in the log call).
- Schema migration runs in `NetworkRepository` constructor or a dedicated `EnsureSchemaAsync` method called from `Program.cs` before the host starts.