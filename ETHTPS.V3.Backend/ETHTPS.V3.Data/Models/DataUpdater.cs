namespace ETHTPS.V3.Data.Models
{
    public sealed class DataUpdater
    {
        public int ID { get; set; }
        public required string Type { get; set; }
        public required string Provider { get; set; }
        public bool Enabled { get; set; }
    }
}
