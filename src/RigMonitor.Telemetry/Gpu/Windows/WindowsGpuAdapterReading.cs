namespace RigMonitor.Telemetry.Gpu.Windows
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One hardware GPU adapter as seen through DXGI, PDH, and D3DKMT.
    /// </summary>
    public sealed class WindowsGpuAdapterReading
    {
        /// <summary>
        /// Enumeration index.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// PCI vendor identifier.
        /// </summary>
        public uint VendorId { get; set; } = 0U;

        /// <summary>
        /// PCI device identifier.
        /// </summary>
        public uint DeviceId { get; set; } = 0U;

        /// <summary>
        /// Adapter description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Adapter LUID formatted as 0xHIGH_0xLOW, matching PDH instance names.
        /// </summary>
        public string Luid { get; set; } = String.Empty;

        /// <summary>
        /// Driver version when known.
        /// </summary>
        public string DriverVersion { get; set; } = String.Empty;

        /// <summary>
        /// Dedicated video memory in bytes (DXGI adapter description).
        /// </summary>
        public double DedicatedVideoMemoryBytes { get; set; } = 0D;

        /// <summary>
        /// Shared system memory in bytes (DXGI adapter description).
        /// </summary>
        public double SharedSystemMemoryBytes { get; set; } = 0D;

        /// <summary>
        /// Local (dedicated) segment usage in bytes.
        /// </summary>
        public double? LocalUsageBytes { get; set; } = null;

        /// <summary>
        /// Local (dedicated) segment budget in bytes.
        /// </summary>
        public double? LocalBudgetBytes { get; set; } = null;

        /// <summary>
        /// Non-local (shared) segment usage in bytes.
        /// </summary>
        public double? NonLocalUsageBytes { get; set; } = null;

        /// <summary>
        /// Non-local (shared) segment budget in bytes.
        /// </summary>
        public double? NonLocalBudgetBytes { get; set; } = null;

        /// <summary>
        /// Utilization percentage per engine type (3d, compute, copy, videoDecode, videoEncode).
        /// </summary>
        public Dictionary<string, double> EngineUtilization { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Current GPU frequency in MHz (D3DKMT node perf data).
        /// </summary>
        public double? CurrentFrequencyMHz { get; set; } = null;

        /// <summary>
        /// Maximum GPU frequency in MHz (D3DKMT node perf data).
        /// </summary>
        public double? MaxFrequencyMHz { get; set; } = null;

        /// <summary>
        /// Memory frequency in MHz (D3DKMT adapter perf data).
        /// </summary>
        public double? MemoryFrequencyMHz { get; set; } = null;

        /// <summary>
        /// Temperature in Celsius (D3DKMT adapter perf data).
        /// </summary>
        public double? TemperatureCelsius { get; set; } = null;
    }

    /// <summary>
    /// Source of Windows GPU adapter readings.
    /// </summary>
    public interface IWindowsGpuAdapterSource
    {
        /// <summary>
        /// Whether DXGI enumeration works on this host.
        /// </summary>
        /// <returns>True when available.</returns>
        bool IsAvailable();

        /// <summary>
        /// Current adapter readings (utilization comes from the background sampler).
        /// </summary>
        /// <returns>Hardware adapters.</returns>
        IReadOnlyList<WindowsGpuAdapterReading> GetAdapters();
    }

    /// <summary>
    /// Well-known PCI vendor identifiers.
    /// </summary>
    public static class PciVendorIds
    {
        /// <summary>
        /// AMD / ATI.
        /// </summary>
        public const uint Amd = 0x1002U;

        /// <summary>
        /// NVIDIA.
        /// </summary>
        public const uint Nvidia = 0x10DEU;

        /// <summary>
        /// Intel.
        /// </summary>
        public const uint Intel = 0x8086U;

        /// <summary>
        /// Qualcomm.
        /// </summary>
        public const uint Qualcomm = 0x5143U;

        /// <summary>
        /// Qualcomm (alternate ACPI-style identifier used by some Adreno drivers).
        /// </summary>
        public const uint QualcommAlternate = 0x4D4FU;
    }
}
