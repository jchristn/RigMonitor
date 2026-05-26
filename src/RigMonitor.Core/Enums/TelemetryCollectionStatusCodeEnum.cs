namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// Stable status codes for telemetry section collection.
    /// </summary>
    public enum TelemetryCollectionStatusCodeEnum
    {
        /// <summary>
        /// The section was collected successfully.
        /// </summary>
        Ok,
        /// <summary>
        /// The section was intentionally not requested.
        /// </summary>
        Disabled,
        /// <summary>
        /// The section is unsupported on the current host.
        /// </summary>
        Unsupported,
        /// <summary>
        /// The section is supported but no current sample was available.
        /// </summary>
        Unavailable,
        /// <summary>
        /// The section collection attempt failed.
        /// </summary>
        Error,
        /// <summary>
        /// The section has no recent successful sample within the freshness window.
        /// </summary>
        Stale
    }
}
