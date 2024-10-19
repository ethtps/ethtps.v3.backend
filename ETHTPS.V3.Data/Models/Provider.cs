namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents a provider entity with details such as type, color, and aggregation information.
    /// </summary>
    public class Provider
    {
        /// <summary>
        /// Gets or sets the unique identifier for the provider.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the name of the provider.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of the provider.
        /// </summary>
        public required int Type { get; set; }

        /// <summary>
        /// Gets or sets the color associated with the provider.
        /// </summary>
        public required string Color { get; set; }

        /// <summary>
        /// Indicates whether the provider is general-purpose.
        /// </summary>
        public bool? IsGeneralPurpose { get; set; }

        /// <summary>
        /// Gets or sets the historical aggregation delta block for the provider.
        /// </summary>
        public int? HistoricalAggregationDeltaBlock { get; set; }

        /// <summary>
        /// Indicates whether the provider is enabled.
        /// </summary>
        public required bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the ID of the parent provider if this is a subchain.
        /// </summary>
        public int? SubchainOf { get; set; }

        /// <summary>
        /// Indicates whether the provider is pending approval.
        /// </summary>
        public bool? PendingApproval { get; set; }

        // Navigation properties

        /// <summary>
        /// Gets or sets the details of the provider type.
        /// </summary>
        public ProviderType? ProviderTypeDetails { get; set; }

        /// <summary>
        /// Gets or sets the parent provider details.
        /// </summary>
        public Provider? ParentProvider { get; set; }
    }
}