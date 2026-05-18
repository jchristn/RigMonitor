namespace RigMonitor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// GPU telemetry response.
    /// </summary>
    public class GpuTelemetry
    {
        /// <summary>
        /// GPU vendor name.
        /// </summary>
        public string Vendor { get; set; } = "NVIDIA";

        /// <summary>
        /// Exporter endpoint used for collection.
        /// </summary>
        public string ExporterEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// GPU devices.
        /// </summary>
        public List<GpuDeviceTelemetry> Devices { get; set; } = new List<GpuDeviceTelemetry>();
    }
}
