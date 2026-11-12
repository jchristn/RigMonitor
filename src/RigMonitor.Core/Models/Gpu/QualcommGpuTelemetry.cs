namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Qualcomm GPU section.
    /// </summary>
    public class QualcommGpuTelemetry
    {
        /// <summary>
        /// Telemetry source.
        /// </summary>
        public GpuTelemetrySourceEnum Source { get; set; } = GpuTelemetrySourceEnum.Unknown;

        /// <summary>
        /// Sysfs root or native interface used for collection.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Qualcomm devices.
        /// </summary>
        public List<QualcommGpuDeviceTelemetry> Devices { get; set; } = new List<QualcommGpuDeviceTelemetry>();
    }

    /// <summary>
    /// Qualcomm Adreno GPU device.
    /// </summary>
    public class QualcommGpuDeviceTelemetry : GpuDeviceTelemetry
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public QualcommGpuDeviceTelemetry()
        {
            Vendor = GpuVendorEnum.Qualcomm;
            UnifiedMemory = true;
        }

        /// <summary>
        /// Windows adapter LUID.
        /// </summary>
        public string? AdapterLuid { get; set; } = null;

        /// <summary>
        /// DXGI device identifier.
        /// </summary>
        public string? DxgiDeviceId { get; set; } = null;

        /// <summary>
        /// Linux device-tree compatible string.
        /// </summary>
        public string? Compatible { get; set; } = null;

        /// <summary>
        /// What the reported utilization covers.
        /// </summary>
        public GpuUtilizationScopeEnum? UtilizationScope { get; set; } = null;

        /// <summary>
        /// Utilization per engine type (3d, compute, copy, videoDecode, videoEncode).
        /// </summary>
        public Dictionary<string, double> EngineUtilization { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Shared (system) memory used by the GPU in megabytes.
        /// </summary>
        public double? SharedMemoryUsedMegabytes { get => _SharedMemoryUsedMegabytes; set => _SharedMemoryUsedMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Shared (system) memory budget for the GPU in megabytes.
        /// </summary>
        public double? SharedMemoryBudgetMegabytes { get => _SharedMemoryBudgetMegabytes; set => _SharedMemoryBudgetMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Dedicated memory used in megabytes.
        /// </summary>
        public double? DedicatedMemoryUsedMegabytes { get => _DedicatedMemoryUsedMegabytes; set => _DedicatedMemoryUsedMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Dedicated memory budget in megabytes.
        /// </summary>
        public double? DedicatedMemoryBudgetMegabytes { get => _DedicatedMemoryBudgetMegabytes; set => _DedicatedMemoryBudgetMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Current GPU frequency in MHz.
        /// </summary>
        public double? CurrentFrequencyMHz { get => _CurrentFrequencyMHz; set => _CurrentFrequencyMHz = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Maximum GPU frequency in MHz.
        /// </summary>
        public double? MaxFrequencyMHz { get => _MaxFrequencyMHz; set => _MaxFrequencyMHz = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Hottest GPU thermal zone in Celsius.
        /// </summary>
        public double? MaxTemperatureCelsius { get => _MaxTemperatureCelsius; set => _MaxTemperatureCelsius = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Which source supplied each field.
        /// </summary>
        public Dictionary<string, GpuTelemetrySourceEnum> FieldSources { get; set; } = new Dictionary<string, GpuTelemetrySourceEnum>(StringComparer.Ordinal);

        private double? _SharedMemoryUsedMegabytes = null;
        private double? _SharedMemoryBudgetMegabytes = null;
        private double? _DedicatedMemoryUsedMegabytes = null;
        private double? _DedicatedMemoryBudgetMegabytes = null;
        private double? _CurrentFrequencyMHz = null;
        private double? _MaxFrequencyMHz = null;
        private double? _MaxTemperatureCelsius = null;
    }
}
