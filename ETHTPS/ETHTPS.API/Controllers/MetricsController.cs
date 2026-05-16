using ETHTPS.API.Models.Responses;
using ETHTPS.API.Services;
using ETHTPS.API.Workers;
using Microsoft.AspNetCore.Mvc;

namespace ETHTPS.API.Controllers;

[ApiController]
[Route("api/v1/metrics")]
public class MetricsController(
    MetricsQueryService metricsService,
    MetricsEventConsumer consumer,
    ConnectionTracker connectionTracker) : ControllerBase
{
    private static readonly Dictionary<string, TimeSpan> MaxRanges = new()
    {
        ["1m"] = TimeSpan.FromHours(24),
        ["5m"] = TimeSpan.FromDays(7),
        ["1h"] = TimeSpan.FromDays(90),
        ["1d"] = TimeSpan.FromDays(365 * 5)
    };

    [HttpGet("{chainId:int}/live")]
    public async Task<IActionResult> GetLive(int chainId, CancellationToken ct)
    {
        var metrics = await metricsService.GetLiveAsync(chainId, ct);
        if (metrics is null)
            return Problem(detail: $"No live data for chain {chainId}.", statusCode: 404);
        return Ok(metrics);
    }

    [HttpGet("{chainId:int}/history")]
    public async Task<IActionResult> GetHistory(
        int chainId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string resolution,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(resolution) || !MaxRanges.TryGetValue(resolution, out var maxRange))
            return Problem(detail: "resolution must be one of: 1m, 5m, 1h, 1d", statusCode: 400);

        if (to <= from)
            return Problem(detail: "'to' must be after 'from'.", statusCode: 400);

        if (to - from > maxRange)
            return Problem(detail: $"Range exceeds maximum of {maxRange.TotalDays:F0} days for resolution '{resolution}'.", statusCode: 400);

        var result = await metricsService.GetHistoryAsync(chainId, from, to, resolution, ct);
        return Ok(result);
    }

    [HttpGet("viewers")]
    public IActionResult GetViewers() => Ok(new { count = connectionTracker.Count });

    [HttpGet("global/live")]
    public IActionResult GetGlobalLive(
        [FromQuery] bool includeTestnets = true,
        [FromQuery] bool includeSidechains = true)
    {
        var (totalTps, totalGps, activeChains, computedAt) = consumer.GetGlobalSnapshot(includeTestnets, includeSidechains);
        return Ok(new GlobalMetricsResponse(totalTps, totalGps, activeChains, computedAt));
    }

    [HttpGet("global/history")]
    public async Task<IActionResult> GetGlobalHistory(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string resolution,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(resolution) || !MaxRanges.TryGetValue(resolution, out var maxRange))
            return Problem(detail: "resolution must be one of: 1m, 5m, 1h, 1d", statusCode: 400);

        if (to <= from)
            return Problem(detail: "'to' must be after 'from'.", statusCode: 400);

        if (to - from > maxRange)
            return Problem(detail: $"Range exceeds maximum of {maxRange.TotalDays:F0} days for resolution '{resolution}'.", statusCode: 400);

        var result = await metricsService.GetGlobalHistoryAsync(from, to, resolution, ct);
        return Ok(result);
    }
}
