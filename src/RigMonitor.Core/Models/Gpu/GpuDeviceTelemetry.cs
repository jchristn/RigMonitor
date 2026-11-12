namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// GPU device telemetry common to every vendor.
    /// </summary>
    public class GpuDeviceTelemetry
    {
        /// <summary>
        /// Globally unique zero-based position across all vendors on the host.
        /// </summary>
        public int Ordinal
        {
            get
            {
                return _Ordinal;
            }
            set
            {
                _Ordinal = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Device index as reported by the vendor source.
        /// </summary>
        public int DeviceIndex
        {
            get
            {
                return _DeviceIndex;
            }
            set
            {
                _DeviceIndex = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Device vendor.
        /// </summary>
        public GpuVendorEnum Vendor { get; set; } = GpuVendorEnum.Unknown;

        /// <summary>
        /// Source that produced this device.
        /// </summary>
        public GpuTelemetrySourceEnum Source { get; set; } = GpuTelemetrySourceEnum.Unknown;

        /// <summary>
        /// Device UUID or other stable identifier.
        /// </summary>
        public string Uuid { get; set; } = String.Empty;

        /// <summary>
        /// Bus identifier (PCI BDF, platform device name, or adapter LUID).
        /// </summary>
        public string BusId { get; set; } = String.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Driver version.
        /// </summary>
        public string DriverVersion { get; set; } = String.Empty;

        /// <summary>
        /// Whether the device uses memory shared with the host.
        /// </summary>
        public bool UnifiedMemory { get; set; } = false;

        /// <summary>
        /// Device metrics.
        /// </summary>
        public GpuUtilizationTelemetry Metrics { get; set; } = new GpuUtilizationTelemetry();

        private int _Ordinal = 0;
        private int _DeviceIndex = 0;
    }
}
