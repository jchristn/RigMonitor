namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Utilyze live telemetry payload.
    /// </summary>
    public class UtilyzeTelemetry
    {
        /// <summary>
        /// Whether Utilyze telemetry is currently available.
        /// </summary>
        public bool Available { get; set; } = false;

        /// <summary>
        /// Utilyze live endpoint used for collection.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Time the Utilyze sample was collected by Utilyze.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Monitored Utilyze device IDs.
        /// </summary>
        public List<int> DeviceIds { get; set; } = new List<int>();

        /// <summary>
        /// Per-device Utilyze samples.
        /// </summary>
        public List<UtilyzeDeviceTelemetry> Devices { get; set; } = new List<UtilyzeDeviceTelemetry>();
    }
}
