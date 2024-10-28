namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents a provider type entity, including details such as name and color.
    /// </summary>
    public sealed class ProviderType
    {
        /// <summary>
        /// Gets or sets the unique identifier for the provider type.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the name of the provider type.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the color associated with the provider type.
        /// </summary>
        public required string Color { get; set; }

        /// <summary>
        /// Indicates whether the provider type is general-purpose.
        /// </summary>
        public bool? IsGeneralPurpose { get; set; }

        /// <summary>
        /// Indicates whether the provider type is enabled.
        /// </summary>
        public required bool Enabled { get; set; }

        // Navigation properties

        /// <summary>
        /// Gets or sets the collection of providers associated with this provider type.
        /// </summary>
        public ICollection<Provider>? Providers { get; set; }
    }
}