namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Collection metadata for a single telemetry section.
    /// </summary>
    public class TelemetrySectionCollectionStatus
    {
        /// <summary>
        /// Whether the section was requested for the current snapshot.
        /// </summary>
        public bool Requested { get; set; } = false;

        /// <summary>
        /// Whether the section is supported on the current host.
        /// </summary>
        public bool Supported { get; set; } = true;

        /// <summary>
        /// Stable section status code.
        /// </summary>
        public TelemetryCollectionStatusCodeEnum StatusCode { get; set; } = TelemetryCollectionStatusCodeEnum.Unavailable;

        /// <summary>
        /// Time of the most recent collection attempt.
        /// </summary>
        public DateTime? LastAttemptUtc { get; set; } = null;

        /// <summary>
        /// Time of the most recent successful collection attempt.
        /// </summary>
        public DateTime? LastSuccessUtc { get; set; } = null;

        /// <summary>
        /// Duration in milliseconds of the most recent collection attempt.
        /// </summary>
        public double? LastDurationMs { get; set; } = null;

        /// <summary>
        /// Freshness details for the most recent successful sample.
        /// </summary>
        public TelemetrySectionFreshness? Freshness { get; set; } = null;

        /// <summary>
        /// Human-readable section status summary.
        /// </summary>
        public string? Message { get; set; } = null;

        /// <summary>
        /// The most recent error observed while collecting the section.
        /// </summary>
        public string? LastError { get; set; } = null;
    }
}
