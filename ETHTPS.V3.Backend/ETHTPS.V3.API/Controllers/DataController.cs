using ETHTPS.V3.Cache.Core;
using ETHTPS.V3.Data.Models;

using Microsoft.AspNetCore.Mvc;

namespace ETHTPS.V3.API.Controllers
{
    [Route("api/[controller]/[action]")]
    public class DataController : Controller
    {
        private readonly AsyncCacheService _cacheService;

        public DataController(AsyncCacheService cacheService)
        {
            _cacheService = cacheService;
        }

        [HttpGet]
        public async Task<IEnumerable<ProviderInfo>?> GetAllProviders()
        {
            return await _cacheService.GetAsync<IEnumerable<ProviderInfo>>("all_providers");
        }
    }
}
