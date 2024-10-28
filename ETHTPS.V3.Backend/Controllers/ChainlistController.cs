using Chainlist.API;

using Microsoft.AspNetCore.Mvc;

namespace ETHTPS.V3.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChainlistController : ControllerBase
    {
        private readonly ChainlistClient _chainlistClient;

        public ChainlistController(ChainlistClient chainlistClient)
        {
            _chainlistClient = chainlistClient;
        }

        [HttpGet("chains")]
        public async Task<IActionResult> GetAllChains()
        {
            try
            {
                var chains = await _chainlistClient.GetAllChainsAsync();
                return Ok(chains);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, $"Error fetching chain data: {ex.Message}");
            }
        }

        [HttpGet("chains/{chainId}")]
        public async Task<IActionResult> GetChainById(int chainId)
        {
            try
            {
                var chain = await _chainlistClient.GetChainByIdAsync(chainId);
                return Ok(chain);
            }
            catch (HttpRequestException ex)
            {
                return NotFound($"Chain with ID {chainId} not found: {ex.Message}");
            }
        }
    }
}
