namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Per-device Utilyze telemetry.
    /// </summary>
    public class UtilyzeDeviceTelemetry
    {
        /// <summary>
        /// Physical GPU device index.
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
        /// Whether the device had a recent live sample.
        /// </summary>
        public bool Online { get; set; } = false;

        /// <summary>
        /// Compute speed-of-light utilization percentage.
        /// </summary>
        public double? ComputeSolPercent { get; set; } = null;

        /// <summary>
        /// Memory speed-of-light utilization percentage.
        /// </summary>
        public double? MemorySolPercent { get; set; } = null;

        /// <summary>
        /// SM active percentage when available.
        /// </summary>
        public double? SmActivePercent { get; set; } = null;

        /// <summary>
        /// NVML GPU utilization percentage when available.
        /// </summary>
        public double? NvmlUtilizationPercent { get; set; } = null;

        /// <summary>
        /// PCIe transmit bytes per second.
        /// </summary>
        public double? PcieTransmitBytesPerSecond { get; set; } = null;

        /// <summary>
        /// PCIe receive bytes per second.
        /// </summary>
        public double? PcieReceiveBytesPerSecond { get; set; } = null;

        /// <summary>
        /// NVLink transmit bytes per second.
        /// </summary>
        public double? NvlinkTransmitBytesPerSecond { get; set; } = null;

        /// <summary>
        /// NVLink receive bytes per second.
        /// </summary>
        public double? NvlinkReceiveBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Inference model name attributed by Utilyze when available.
        /// </summary>
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Attainable compute SOL ceiling percentage for the attributed workload.
        /// </summary>
        public double? ComputeSolCeilingPercent { get; set; } = null;

        private int _DeviceIndex = 0;
    }
}
