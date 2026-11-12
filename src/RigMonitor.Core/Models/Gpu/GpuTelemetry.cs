namespace RigMonitor.Core.Models
{
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Vendor-neutral GPU aggregate derived from the NVIDIA, AMD, Apple, and Qualcomm sections.
    /// </summary>
    public class GpuTelemetry
    {
        /// <summary>
        /// Vendor present on the host, or mixed when more than one vendor is present.
        /// </summary>
        public GpuVendorEnum Vendor { get; set; } = GpuVendorEnum.Unknown;

        /// <summary>
        /// Vendors that contributed at least one device.
        /// </summary>
        public List<GpuVendorEnum> Vendors { get; set; } = new List<GpuVendorEnum>();

        /// <summary>
        /// All GPU devices in their common shape, ordered by vendor and native index.
        /// </summary>
        public List<GpuDeviceTelemetry> Devices { get; set; } = new List<GpuDeviceTelemetry>();
    }
}
