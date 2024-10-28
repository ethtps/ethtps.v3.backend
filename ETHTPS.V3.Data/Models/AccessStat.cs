namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents access statistics for a specific project and path.
    /// </summary>
    public sealed class AccessStat
    {
        /// <summary>
        /// Gets or sets the unique identifier for the access statistic.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the project associated with the access statistic.
        /// </summary>
        public required string Project { get; set; }

        /// <summary>
        /// Gets or sets the path for which the access statistic is recorded.
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// Gets or sets the count of access occurrences for the path.
        /// </summary>
        public required int Count { get; set; }

        /// <summary>
        /// Gets or sets the average request time in milliseconds for the path.
        /// </summary>
        public required float AverageRequestTimeMs { get; set; }
    }
}