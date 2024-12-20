using ETHTPS.V3.Data.Mongo;
using ETHTPS.V3.Data.Mongo.Models;

using Microsoft.AspNetCore.Mvc;

namespace ETHTPS.V3.Backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DataController : ControllerBase
    {
        private readonly ETHTPSConnection _connection;

        public DataController(ETHTPSConnection connection)
        {
            _connection = connection;
        }

        [HttpGet("all-chains")]
        public async Task<List<ChainInfo>> GetAllChains() => await _connection.GetAllChainsAsync();
    }
}
