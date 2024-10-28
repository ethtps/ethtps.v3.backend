namespace ETHTPS.V3.Data.Models
{
    /// <summary>
    /// Represents a binding between an endpoint and an updater.
    /// </summary>
    public sealed class Binding
    {
        /// <summary>
        /// Gets or sets the unique identifier for the binding.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the endpoint identifier associated with the binding.
        /// </summary>
        public required int Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the updater identifier associated with the binding.
        /// </summary>
        public required int Updater { get; set; }

        /// <summary>
        /// Indicates whether the binding is active.
        /// </summary>
        public bool? IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the last error encountered for this binding.
        /// </summary>
        public string? LastError { get; set; }
    }
}