namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents an updater entity that updates network and provider information.
    /// </summary>
    public class Updater
    {
        /// <summary>
        /// Gets or sets the unique identifier for the updater.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the network identifier associated with the updater.
        /// </summary>
        public required int Network { get; set; }

        /// <summary>
        /// Gets or sets the provider identifier associated with the updater.
        /// </summary>
        public required int Provider { get; set; }

        /// <summary>
        /// Gets or sets a description of the updater.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the last update.
        /// </summary>
        public DateTime? LastUpdated { get; set; }
    }
}