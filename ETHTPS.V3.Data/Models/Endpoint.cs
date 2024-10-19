namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents an endpoint entity with address and authentication details.
    /// </summary>
    public class Endpoint
    {
        /// <summary>
        /// Gets or sets the unique identifier for the endpoint.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the address of the endpoint.
        /// </summary>
        public required string Address { get; set; }

        /// <summary>
        /// Indicates whether the endpoint is enabled.
        /// </summary>
        public required bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a description of the endpoint.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the authentication type used by the endpoint.
        /// </summary>
        public string? AuthType { get; set; }
    }
}