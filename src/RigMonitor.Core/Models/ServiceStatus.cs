namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Generic service status payload.
    /// </summary>
    public class ServiceStatus
    {
        /// <summary>
        /// Status string.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Whether the service is ready for traffic.
        /// </summary>
        public bool Ready { get; set; } = false;

        /// <summary>
        /// Optional human-readable message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Timestamp of the status payload.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
