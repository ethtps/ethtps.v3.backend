namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents the status of the health of a binding.
    /// </summary>
    public sealed class HealthStatus
    {
        /// <summary>
        /// Gets or sets the unique identifier for the health status.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the name of the health status.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets additional details about the health status.
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the health status.
        /// </summary>
        public int? SeverityLevel { get; set; }
    }
}