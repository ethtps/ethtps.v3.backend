using System.Text.Json;
using ETHTPS.API.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace ETHTPS.API.Controllers;

[ApiController]
[Route("api/v1/ingestion")]
public class IngestionController(IConnectionMultiplexer redis) : ControllerBase
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(120);

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync("ethtps:ingestion:status");

        if (!value.HasValue)
            return Problem(detail: "Ingestion status is not available. The ingestion service may be down or starting up.", statusCode: 503);

        var raw = JsonSerializer.Deserialize<RawChainStatus[]>((string)value!, JsonOptions);
        if (raw is null || raw.Length == 0)
            return Problem(detail: "Ingestion status is empty.", statusCode: 503);

        var now = DateTimeOffset.UtcNow;
        var chains = raw.Select(c => new ChainIngestionStatus(
            c.ChainId,
            c.Name,
            c.LastBlockNumber,
            c.LastTxCount,
            c.LastBlockTimeMs,
            c.LastBlockAt,
            c.State,
            c.State == "ok" && now - c.LastBlockAt > StaleThreshold
        )).ToList();

        return Ok(new IngestionStatusResponse(now, chains.Count, chains));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record RawChainStatus(
        int ChainId,
        string Name,
        long LastBlockNumber,
        int LastTxCount,
        long LastBlockTimeMs,
        DateTimeOffset LastBlockAt,
        string State);
}
