using System.Text.Json.Serialization;

namespace ETHTPS.ChainRegistry.Clients;

public class ChainlistNetwork
{
    [JsonPropertyName("chainId")]
    public long ChainId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("rpc")]
    public string[] Rpc { get; set; } = [];

    [JsonPropertyName("network")]
    public string Network { get; set; } = "";
}
