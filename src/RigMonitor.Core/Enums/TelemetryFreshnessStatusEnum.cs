namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// Freshness evaluation for the last successful section sample.
    /// </summary>
    public enum TelemetryFreshnessStatusEnum
    {
        /// <summary>
        /// The last successful sample falls within the configured freshness window.
        /// </summary>
        Fresh,
        /// <summary>
        /// The last successful sample exceeds the configured freshness window.
        /// </summary>
        Stale,
        /// <summary>
        /// No successful sample is available to evaluate.
        /// </summary>
        Unknown,
        /// <summary>
        /// Freshness does not apply because the section was not requested or is unsupported.
        /// </summary>
        NotApplicable
    }
}
