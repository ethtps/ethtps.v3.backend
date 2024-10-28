namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents health information for a specific binding.
    /// </summary>
    public sealed class Health
    {
        /// <summary>
        /// Gets or sets the unique identifier for the health entity.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the binding identifier associated with this health record.
        /// </summary>
        public required int Binding { get; set; }

        /// <summary>
        /// Gets or sets the health status identifier associated with this health record.
        /// </summary>
        public required int Status { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the last health check.
        /// </summary>
        public required DateTime LastCheck { get; set; }

        /// <summary>
        /// Gets or sets the response time in milliseconds for the last health check.
        /// </summary>
        public int? ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the details of any error encountered during the health check.
        /// </summary>
        public string? ErrorDetails { get; set; }
    }
}