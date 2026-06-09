using ETHTPS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ETHTPS.API.Controllers;

[ApiController]
[Route("api/v1/networks")]
public class NetworksController(NetworkQueryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var networks = await service.GetAllActiveAsync(ct);
        return Ok(networks);
    }

    [HttpGet("{chainId:int}")]
    public async Task<IActionResult> GetByChainId(int chainId, CancellationToken ct)
    {
        var network = await service.GetByChainIdAsync(chainId, ct);
        if (network is null)
            return Problem(detail: $"Network with chain ID {chainId} not found.", statusCode: 404);
        return Ok(network);
    }

    [HttpGet("{chainId:int}/logo")]
    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> GetLogo(int chainId, CancellationToken ct)
    {
        var logo = await service.GetLogoAsync(chainId, ct);
        if (logo is null)
            return Problem(detail: $"No logo available for chain {chainId}.", statusCode: 404);
        return File(logo.Value.Bytes, logo.Value.ContentType);
    }
}
