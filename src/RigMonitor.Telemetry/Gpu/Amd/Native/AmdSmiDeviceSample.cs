namespace RigMonitor.Telemetry.Gpu.Amd.Native
{
    using System;

    /// <summary>
    /// Raw per-device values read from the AMD SMI library. Null means the library reported the value as unsupported or failed.
    /// </summary>
    public sealed class AmdSmiDeviceSample
    {
        /// <summary>
        /// Zero-based enumeration index.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// PCI bus/device/function in domain:bus:device.function form.
        /// </summary>
        public string BusId { get; set; } = String.Empty;

        /// <summary>
        /// Device UUID.
        /// </summary>
        public string Uuid { get; set; } = String.Empty;

        /// <summary>
        /// Marketing name.
        /// </summary>
        public string MarketName { get; set; } = String.Empty;

        /// <summary>
        /// Compute partition mode.
        /// </summary>
        public string? ComputePartition { get; set; } = null;

        /// <summary>
        /// Memory partition mode.
        /// </summary>
        public string? MemoryPartition { get; set; } = null;

        /// <summary>
        /// Graphics activity percent.
        /// </summary>
        public double? GfxActivityPercent { get; set; } = null;

        /// <summary>
        /// Memory controller activity percent.
        /// </summary>
        public double? UmcActivityPercent { get; set; } = null;

        /// <summary>
        /// Multimedia activity percent.
        /// </summary>
        public double? MmActivityPercent { get; set; } = null;

        /// <summary>
        /// VRAM total bytes.
        /// </summary>
        public double? VramTotalBytes { get; set; } = null;

        /// <summary>
        /// VRAM used bytes.
        /// </summary>
        public double? VramUsedBytes { get; set; } = null;

        /// <summary>
        /// GTT total bytes.
        /// </summary>
        public double? GttTotalBytes { get; set; } = null;

        /// <summary>
        /// GTT used bytes.
        /// </summary>
        public double? GttUsedBytes { get; set; } = null;

        /// <summary>
        /// Edge temperature in Celsius.
        /// </summary>
        public double? EdgeTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Hotspot temperature in Celsius.
        /// </summary>
        public double? HotspotTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Memory temperature in Celsius.
        /// </summary>
        public double? MemoryTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Graphics clock in MHz.
        /// </summary>
        public double? GfxClockMHz { get; set; } = null;

        /// <summary>
        /// Memory clock in MHz.
        /// </summary>
        public double? MemoryClockMHz { get; set; } = null;

        /// <summary>
        /// Correctable ECC errors.
        /// </summary>
        public long? CorrectableEccErrors { get; set; } = null;

        /// <summary>
        /// Uncorrectable ECC errors.
        /// </summary>
        public long? UncorrectableEccErrors { get; set; } = null;
    }
}
