namespace RigMonitor.Core.Models
{
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Result of probing a GPU vendor provider at startup.
    /// </summary>
    public class GpuProbeResult
    {
        /// <summary>
        /// Vendor that was probed.
        /// </summary>
        public GpuVendorEnum Vendor { get; set; } = GpuVendorEnum.Unknown;

        /// <summary>
        /// Whether the vendor has at least one device reachable through a supported source.
        /// </summary>
        public bool Available { get; set; } = false;

        /// <summary>
        /// Resolved primary source.
        /// </summary>
        public GpuTelemetrySourceEnum? Source { get; set; } = null;

        /// <summary>
        /// Endpoint, library path, or root used by the resolved source.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Human-readable detail (for example library version or gap-fill source).
        /// </summary>
        public string? Detail { get; set; } = null;

        /// <summary>
        /// Optional native features that loaded.
        /// </summary>
        public List<string> Features { get; set; } = new List<string>();

        /// <summary>
        /// Create an unavailable result.
        /// </summary>
        /// <param name="vendor">Vendor.</param>
        /// <param name="detail">Reason.</param>
        /// <returns>Result.</returns>
        public static GpuProbeResult Unavailable(GpuVendorEnum vendor, string? detail = null)
        {
            return new GpuProbeResult { Vendor = vendor, Available = false, Detail = detail };
        }
    }
}
