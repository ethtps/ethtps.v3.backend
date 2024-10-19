namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents detailed access statistics, including request time, IP address, and date.
    /// </summary>
    public class DetailedAccessStat
    {
        /// <summary>
        /// Gets or sets the unique identifier for the detailed access statistic.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the path for which the detailed access statistic is recorded.
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// Gets or sets the request time in milliseconds for the path.
        /// </summary>
        public required float RequestTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the IP address from which the request originated.
        /// </summary>
        public required string IPAddress { get; set; }

        /// <summary>
        /// Gets or sets the date of the request.
        /// </summary>
        public required DateTime Date { get; set; }
    }
}