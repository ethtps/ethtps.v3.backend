using Newtonsoft.Json;

namespace Chainlist.API
{
    /// <summary>
    /// Represents a class that contains information about the native currency of a chain.
    /// </summary>
    public sealed class NativeCurrency
    {
        [JsonProperty("name")]
        public required string Name { get; set; }

        [JsonProperty("symbol")]
        public required string Symbol { get; set; }

        [JsonProperty("decimals")]
        public int Decimals { get; set; }
    }
}
