namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents the configuration settings for an updater.
    /// </summary>
    public sealed class UpdaterConfiguration
    {
        /// <summary>
        /// Gets or sets the unique identifier for the updater configuration.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the updater identifier associated with this configuration.
        /// </summary>
        public required int Updater { get; set; }

        /// <summary>
        /// Indicates whether the updater is enabled.
        /// </summary>
        public required bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the interval in milliseconds between updates.
        /// </summary>
        public required int UpdateIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the maximum number of retries for the updater.
        /// </summary>
        public int? MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets the interval in milliseconds between retries.
        /// </summary>
        public int? RetryIntervalMs { get; set; }

        /// <summary>
        /// Gets or sets the authentication method used by the updater.
        /// </summary>
        public string? AuthMethod { get; set; }

        /// <summary>
        /// Gets or sets the details of the authentication method.
        /// </summary>
        public string? AuthMethodDetails { get; set; }
    }
}