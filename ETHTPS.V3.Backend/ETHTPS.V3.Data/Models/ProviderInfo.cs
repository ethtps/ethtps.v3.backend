namespace ETHTPS.V3.Data.Models
{
    public class ProviderInfo
    {
        public int ID { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public string? InfoURL { get; set; }
        public required string NativeCurrency { get; set; }
        public required string NativeCurrencySymbol { get; set; }
    }
}
