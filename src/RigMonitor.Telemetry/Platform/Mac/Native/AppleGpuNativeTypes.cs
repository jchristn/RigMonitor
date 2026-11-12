namespace RigMonitor.Telemetry.Platform.Mac.Native
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One IOAccelerator registry entry.
    /// </summary>
    public sealed class AppleAcceleratorInfo
    {
        /// <summary>
        /// Registry entry name.
        /// </summary>
        public string RegistryName { get; set; } = String.Empty;

        /// <summary>
        /// IOClass (for example AGXAcceleratorG17X, AMDRadeonX6000_AMDNavi10GraphicsAccelerator).
        /// </summary>
        public string IOClass { get; set; } = String.Empty;

        /// <summary>
        /// Model property.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// gpu-core-count property.
        /// </summary>
        public int? CoreCount { get; set; } = null;

        /// <summary>
        /// Numeric PerformanceStatistics values.
        /// </summary>
        public Dictionary<string, double> PerformanceStatistics { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Values that change rarely (Metal device properties, frequency table, host memory).
    /// </summary>
    public sealed class AppleGpuStaticInfo
    {
        /// <summary>
        /// Metal device name.
        /// </summary>
        public string? MetalDeviceName { get; set; } = null;

        /// <summary>
        /// Whether the Metal device reports unified memory.
        /// </summary>
        public bool? HasUnifiedMemory { get; set; } = null;

        /// <summary>
        /// Metal recommendedMaxWorkingSetSize in bytes.
        /// </summary>
        public double? RecommendedMaxWorkingSetBytes { get; set; } = null;

        /// <summary>
        /// Highest supported Metal GPU family (for example "apple10" or "mac2").
        /// </summary>
        public string? MetalFamily { get; set; } = null;

        /// <summary>
        /// Host physical memory in bytes (hw.memsize).
        /// </summary>
        public double? HostMemoryBytes { get; set; } = null;

        /// <summary>
        /// GPU DVFS frequency table in Hz, ascending, including the zero (off) state when present.
        /// </summary>
        public List<double> GpuFrequenciesHz { get; set; } = new List<double>();
    }

    /// <summary>
    /// IOReport delta between two samples.
    /// </summary>
    public sealed class AppleIOReportDelta
    {
        /// <summary>
        /// Seconds between the two samples.
        /// </summary>
        public double ElapsedSeconds { get; set; } = 0D;

        /// <summary>
        /// GPU energy consumed in joules, when the channel is present.
        /// </summary>
        public double? GpuEnergyJoules { get; set; } = null;

        /// <summary>
        /// GPU performance-state residencies in order (state name, residency ticks).
        /// </summary>
        public List<KeyValuePair<string, long>> GpuStateResidencies { get; set; } = new List<KeyValuePair<string, long>>();
    }

    /// <summary>
    /// Seam over the native macOS frameworks used for Apple GPU telemetry.
    /// </summary>
    public interface IAppleGpuNative : IDisposable
    {
        /// <summary>
        /// Read IOAccelerator entries through IOKit.
        /// </summary>
        /// <returns>Accelerators.</returns>
        List<AppleAcceleratorInfo> ReadAccelerators();

        /// <summary>
        /// Read rarely changing values (Metal, sysctl, pmgr frequency table).
        /// </summary>
        /// <returns>Static info.</returns>
        AppleGpuStaticInfo ReadStatic();

        /// <summary>
        /// Read iogpu.wired_limit_mb (0 means OS default).
        /// </summary>
        /// <returns>Limit in megabytes, or null when unavailable.</returns>
        long? ReadWiredLimitMegabytes();

        /// <summary>
        /// Read NSProcessInfo.thermalState (0 nominal .. 3 critical).
        /// </summary>
        /// <returns>Thermal state, or null when unavailable.</returns>
        int? ReadThermalState();

        /// <summary>
        /// Subscribe to IOReport GPU energy and performance-state channels.
        /// </summary>
        /// <param name="error">Reason on failure.</param>
        /// <returns>True when subscribed.</returns>
        bool TryStartIOReport(out string? error);

        /// <summary>
        /// Take an IOReport sample and return the delta from the previous sample (null on the first call).
        /// </summary>
        /// <returns>Delta, or null.</returns>
        AppleIOReportDelta? SampleIOReport();

        /// <summary>
        /// Read temperature sensors through IOHID (product name, Celsius).
        /// </summary>
        /// <param name="include">Optional filter on sensor product names; only matching sensors are queried.</param>
        /// <returns>Sensors, or null when IOHID is unavailable.</returns>
        List<KeyValuePair<string, double>>? ReadHidTemperatures(Func<string, bool>? include = null);
    }
}
