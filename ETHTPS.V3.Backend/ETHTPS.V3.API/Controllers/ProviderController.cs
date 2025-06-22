using ETHTPS.V3.Data;
using ETHTPS.V3.Data.Models;

using Microsoft.AspNetCore.Mvc;

namespace ETHTPS.V3.API.Controllers
{
    [Route("api/[controller]/[action]")]
    public class ProviderController : Controller
    {
        private readonly ETHTPSDatabase _database;

        public ProviderController(ETHTPSDatabase database)
        {
            _database = database;
        }

        [HttpGet]
        public IEnumerable<ProviderInfo> GetAllProviders()
        {
            return _database.GetAllProviders();
        }
    }
}
