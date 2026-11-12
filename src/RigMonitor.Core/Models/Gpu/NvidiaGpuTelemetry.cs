namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// NVIDIA GPU section.
    /// </summary>
    public class NvidiaGpuTelemetry
    {
        /// <summary>
        /// Telemetry source.
        /// </summary>
        public GpuTelemetrySourceEnum Source { get; set; } = GpuTelemetrySourceEnum.DcgmExporter;

        /// <summary>
        /// Exporter endpoint used for collection.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// NVIDIA devices.
        /// </summary>
        public List<NvidiaGpuDeviceTelemetry> Devices { get; set; } = new List<NvidiaGpuDeviceTelemetry>();
    }

    /// <summary>
    /// NVIDIA GPU device.
    /// </summary>
    public class NvidiaGpuDeviceTelemetry : GpuDeviceTelemetry
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public NvidiaGpuDeviceTelemetry()
        {
            Vendor = GpuVendorEnum.Nvidia;
            Source = GpuTelemetrySourceEnum.DcgmExporter;
        }

        /// <summary>
        /// MIG profile when present.
        /// </summary>
        public string? MigProfile { get; set; } = null;

        /// <summary>
        /// XID errors observed on the device.
        /// </summary>
        public long? XidErrors { get => _XidErrors; set => _XidErrors = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Memory temperature in Celsius.
        /// </summary>
        public double? MemoryTemperatureCelsius { get => _MemoryTemperatureCelsius; set => _MemoryTemperatureCelsius = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Memory copy engine utilization percentage.
        /// </summary>
        public double? MemoryCopyUtilizationPercent { get => _MemoryCopyUtilizationPercent; set => _MemoryCopyUtilizationPercent = GpuMetricMath.Percent(value); }

        /// <summary>
        /// Volatile uncorrectable (double-bit) ECC errors.
        /// </summary>
        public long? UncorrectableEccErrors { get => _UncorrectableEccErrors; set => _UncorrectableEccErrors = GpuMetricMath.NonNegative(value); }

        private long? _XidErrors = null;
        private double? _MemoryTemperatureCelsius = null;
        private double? _MemoryCopyUtilizationPercent = null;
        private long? _UncorrectableEccErrors = null;
    }
}
