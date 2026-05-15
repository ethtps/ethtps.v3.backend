namespace ETHTPS.V3.Data.Models
{
    public sealed class EndpointMetadata
    {
        public int ID { get; set; }
        public bool Enabled { get; set; }
        public bool Healthy { get; set; }
        public required string URL { get; set; }
        public required string Provider { get; set; }
        public DateTime? LastHit { get; set; }
        public int FailureCount { get; set; }
        public int HitCount { get; set; }
        public int AverageAccessTimeMs { get; set; }
    }
}
