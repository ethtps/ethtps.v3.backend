using Newtonsoft.Json;

namespace Chainlist.API
{
    public sealed class ChainInfo
    {
        [JsonProperty("name")]
        public required string Name { get; set; }

        [JsonProperty("chain")]
        public required string Chain { get; set; }

        [JsonProperty("network")]
        public required string Network { get; set; }

        [JsonProperty("rpc")]
        public required string[] RpcUrls { get; set; }

        [JsonProperty("faucets")]
        public required string[] Faucets { get; set; }

        [JsonProperty("nativeCurrency")]
        public required NativeCurrency NativeCurrency { get; set; }

        [JsonProperty("infoURL")]
        public required string InfoURL { get; set; }

        [JsonProperty("shortName")]
        public required string ShortName { get; set; }

        [JsonProperty("chainId")]
        public required int ChainId { get; set; }

        [JsonProperty("networkId")]
        public required int NetworkId { get; set; }
    }
}
