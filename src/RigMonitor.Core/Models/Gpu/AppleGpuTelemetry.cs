namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Apple GPU section.
    /// </summary>
    public class AppleGpuTelemetry
    {
        /// <summary>
        /// Telemetry source.
        /// </summary>
        public GpuTelemetrySourceEnum Source { get; set; } = GpuTelemetrySourceEnum.AppleNative;

        /// <summary>
        /// Chip name (for example "Apple M3 Max").
        /// </summary>
        public string Chip { get; set; } = String.Empty;

        /// <summary>
        /// Total GPU core count.
        /// </summary>
        public int? GpuCoreCount { get; set; } = null;

        /// <summary>
        /// Highest supported Metal GPU family (for example "apple9").
        /// </summary>
        public string? MetalFamily { get; set; } = null;

        /// <summary>
        /// Operating-system thermal state.
        /// </summary>
        public GpuThermalStateEnum? ThermalState { get; set; } = null;

        /// <summary>
        /// What the reported temperature measures: gpuSensors (GPU-specific sensors) or socDie (SoC die sensors shared with the GPU).
        /// </summary>
        public string? TemperatureSource { get; set; } = null;

        /// <summary>
        /// Host physical memory in megabytes.
        /// </summary>
        public double? HostMemoryTotalMegabytes { get; set; } = null;

        /// <summary>
        /// Metal recommended maximum working set in megabytes (how much unified memory the GPU should use).
        /// </summary>
        public double? RecommendedMaxWorkingSetMegabytes { get; set; } = null;

        /// <summary>
        /// Configured GPU wired memory limit in megabytes (iogpu.wired_limit_mb), null when the OS default applies.
        /// </summary>
        public double? WiredLimitMegabytes { get; set; } = null;

        /// <summary>
        /// Native sources that are active (ioKit, metal, ioReport, hidSensors, processInfo).
        /// </summary>
        public List<string> Features { get; set; } = new List<string>();

        /// <summary>
        /// Apple GPU devices.
        /// </summary>
        public List<AppleGpuDeviceTelemetry> Devices { get; set; } = new List<AppleGpuDeviceTelemetry>();
    }

    /// <summary>
    /// Apple GPU device.
    /// </summary>
    public class AppleGpuDeviceTelemetry : GpuDeviceTelemetry
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public AppleGpuDeviceTelemetry()
        {
            Vendor = GpuVendorEnum.Apple;
            Source = GpuTelemetrySourceEnum.AppleNative;
            UnifiedMemory = true;
        }

        /// <summary>
        /// GPU core count.
        /// </summary>
        public int? GpuCoreCount { get; set; } = null;

        /// <summary>
        /// Renderer utilization percentage.
        /// </summary>
        public double? RendererUtilizationPercent { get => _RendererUtilizationPercent; set => _RendererUtilizationPercent = GpuMetricMath.Percent(value); }

        /// <summary>
        /// Tiler utilization percentage.
        /// </summary>
        public double? TilerUtilizationPercent { get => _TilerUtilizationPercent; set => _TilerUtilizationPercent = GpuMetricMath.Percent(value); }

        /// <summary>
        /// System memory allocated by the GPU driver in megabytes.
        /// </summary>
        public double? AllocatedSystemMemoryMegabytes { get => _AllocatedSystemMemoryMegabytes; set => _AllocatedSystemMemoryMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Share of time the GPU spent in an active performance state.
        /// </summary>
        public double? ActiveResidencyPercent { get => _ActiveResidencyPercent; set => _ActiveResidencyPercent = GpuMetricMath.Percent(value); }

        /// <summary>
        /// Hottest GPU sensor in Celsius.
        /// </summary>
        public double? MaxTemperatureCelsius { get => _MaxTemperatureCelsius; set => _MaxTemperatureCelsius = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Metal recommended maximum working set in megabytes.
        /// </summary>
        public double? RecommendedMaxWorkingSetMegabytes { get => _RecommendedMaxWorkingSetMegabytes; set => _RecommendedMaxWorkingSetMegabytes = GpuMetricMath.NonNegative(value); }

        /// <summary>
        /// Operating-system thermal state.
        /// </summary>
        public GpuThermalStateEnum? ThermalState { get; set; } = null;

        private double? _RendererUtilizationPercent = null;
        private double? _TilerUtilizationPercent = null;
        private double? _AllocatedSystemMemoryMegabytes = null;
        private double? _ActiveResidencyPercent = null;
        private double? _MaxTemperatureCelsius = null;
        private double? _RecommendedMaxWorkingSetMegabytes = null;
    }
}
