namespace RigMonitor.Core.Models
{
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Freshness details for the last successful telemetry section sample.
    /// </summary>
    public class TelemetrySectionFreshness
    {
        /// <summary>
        /// Freshness classification.
        /// </summary>
        public TelemetryFreshnessStatusEnum Status { get; set; } = TelemetryFreshnessStatusEnum.Unknown;

        /// <summary>
        /// Age in milliseconds of the last successful sample when available.
        /// </summary>
        public double? AgeMs { get; set; } = null;

        /// <summary>
        /// Configured stale threshold in milliseconds.
        /// </summary>
        public int StaleAfterMs
        {
            get
            {
                return _StaleAfterMs;
            }
            set
            {
                _StaleAfterMs = value < 0 ? 0 : value;
            }
        }

        private int _StaleAfterMs = 0;
    }
}
