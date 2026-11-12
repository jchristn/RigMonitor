namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// AMD GPU section.
    /// </summary>
    public class AmdGpuTelemetry
    {
        /// <summary>
        /// Primary telemetry source.
        /// </summary>
        public GpuTelemetrySourceEnum Source { get; set; } = GpuTelemetrySourceEnum.Unknown;

        /// <summary>
        /// Source used to fill fields the primary source could not provide, when any.
        /// </summary>
        public GpuTelemetrySourceEnum? GapFillSource { get; set; } = null;

        /// <summary>
        /// AMD SMI library version when the native library is in use.
        /// </summary>
        public string? LibraryVersion { get; set; } = null;

        /// <summary>
        /// Library path, sysfs root, or exporter URL used for collection.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// AMD devices.
        /// </summary>
        public List<AmdGpuDeviceTelemetry> Devices { get; set; } = new List<AmdGpuDeviceTelemetry>();
    }

    /// <summary>
    /// AMD GPU device.
    /// </summary>
    public class AmdGpuDeviceTelemetry : GpuDeviceTelemetry
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public AmdGpuDeviceTelemetry()
        {
            Vendor = GpuVendorEnum.Amd;
        }

        /// <summary>
        /// Hotspot (junction) temperature in Celsius.
        /// </summary>
        public double? HotspotTemperatureCelsius { get => _HotspotTemperatureCelsius; set => _HotspotTemperatureCelsius = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Memory (VRAM/HBM) temperature in Celsius.
        /// </summary>
        public double? MemoryTemperatureCelsius { get => _MemoryTemperatureCelsius; set => _MemoryTemperatureCelsius = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Memory controller activity percentage.
        /// </summary>
        public double? MemoryActivityPercent { get => _MemoryActivityPercent; set => _MemoryActivityPercent = GpuMetricMath.Percent(value); }

        /// <summary>
        /// Multimedia engine activity percentage.
        /// </summary>
        public double? MultimediaActivityPercent { get => _MultimediaActivityPercent; set => _MultimediaActivityPercent = GpuMetricMath.Percent(value); }

        /// <summary>
        /// GTT (GPU-accessible system memory) used in megabytes.
        /// </summary>
        public double? GttUsedMegabytes { get => _GttUsedMegabytes; set => _GttUsedMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// GTT (GPU-accessible system memory) total in megabytes.
        /// </summary>
        public double? GttTotalMegabytes { get => _GttTotalMegabytes; set => _GttTotalMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Uncorrectable ECC error count.
        /// </summary>
        public long? UncorrectableEccErrors { get => _UncorrectableEccErrors; set => _UncorrectableEccErrors = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Correctable ECC error count.
        /// </summary>
        public long? CorrectableEccErrors { get => _CorrectableEccErrors; set => _CorrectableEccErrors = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Partition identifier when the GPU is partitioned.
        /// </summary>
        public string? PartitionId { get; set; } = null;

        /// <summary>
        /// Compute partition mode (for example SPX or CPX).
        /// </summary>
        public string? ComputePartition { get; set; } = null;

        /// <summary>
        /// Memory partition mode (for example NPS1 or NPS4).
        /// </summary>
        public string? MemoryPartition { get; set; } = null;

        /// <summary>
        /// Which source supplied each field (field name to source).
        /// </summary>
        public Dictionary<string, GpuTelemetrySourceEnum> FieldSources { get; set; } = new Dictionary<string, GpuTelemetrySourceEnum>(StringComparer.Ordinal);

        private double? _HotspotTemperatureCelsius = null;
        private double? _MemoryTemperatureCelsius = null;
        private double? _MemoryActivityPercent = null;
        private double? _MultimediaActivityPercent = null;
        private double? _GttUsedMegabytes = null;
        private double? _GttTotalMegabytes = null;
        private long? _UncorrectableEccErrors = null;
        private long? _CorrectableEccErrors = null;
    }
}
