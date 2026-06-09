using System.Text.Json.Serialization;

namespace ETHTPS.Ingestion.Models;

public class RpcOverride
{
    [JsonPropertyName("networkName")]
    public string NetworkName { get; set; } = "";

    [JsonPropertyName("rpcUrls")]
    public string[] RpcUrls { get; set; } = [];
}
