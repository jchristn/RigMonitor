namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// GPU device telemetry.
    /// </summary>
    public class GpuDeviceTelemetry
    {
        /// <summary>
        /// Device index as reported by the exporter.
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
        /// GPU UUID.
        /// </summary>
        public string Uuid { get; set; } = String.Empty;

        /// <summary>
        /// PCI bus identifier.
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
        /// MIG profile when present.
        /// </summary>
        public string? MigProfile { get; set; } = null;

        /// <summary>
        /// Device metrics.
        /// </summary>
        public GpuUtilizationTelemetry Metrics { get; set; } = new GpuUtilizationTelemetry();

        private int _DeviceIndex = 0;
    }
}
