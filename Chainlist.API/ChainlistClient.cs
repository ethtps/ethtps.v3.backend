using Newtonsoft.Json;

namespace Chainlist.API
{
    /// <summary>
    /// Represents a class that contains information about a chain. This class is based on the Chainlist API.
    /// </summary>
    public sealed class ChainlistClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ChainlistClient(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(baseUrl);
            _baseUrl = baseUrl;
        }

        public async Task<ChainInfo[]> GetAllChainsAsync()
        {
            var response = await _httpClient.GetAsync("/chains.json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ChainInfo[]>(content)!;
        }

        public async Task<ChainInfo> GetChainByIdAsync(int chainId)
        {
            var response = await _httpClient.GetAsync($"/chains/{chainId}.json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ChainInfo>(content)!;
        }
    }
}
