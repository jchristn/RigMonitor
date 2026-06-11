namespace RigMonitor.Core.Models
{
    /// <summary>
    /// Detailed persisted telemetry sample.
    /// </summary>
    public class TelemetrySampleDetail : TelemetrySampleRecord
    {
        /// <summary>
        /// Original telemetry snapshot.
        /// </summary>
        public TelemetrySnapshot Snapshot { get; set; } = new TelemetrySnapshot();
    }
}
