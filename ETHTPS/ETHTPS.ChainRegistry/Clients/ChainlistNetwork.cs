using System.Text.Json.Serialization;

namespace ETHTPS.ChainRegistry.Clients;

public class ChainlistNetwork
{
    [JsonPropertyName("chainId")]
    public int ChainId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("rpc")]
    public string[] Rpc { get; set; } = [];
}
