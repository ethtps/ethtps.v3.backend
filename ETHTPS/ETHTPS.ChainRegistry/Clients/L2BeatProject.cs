using System.Text.Json.Serialization;

namespace ETHTPS.ChainRegistry.Clients;

public class L2BeatProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("isArchived")]
    public bool IsArchived { get; set; }
}

public class L2BeatResponse
{
    [JsonPropertyName("projects")]
    public Dictionary<string, L2BeatProject>? Projects { get; set; }
}
