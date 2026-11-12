namespace RigMonitor.Telemetry.Gpu.Windows
{
    using System;
    using System.Globalization;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Maps Windows adapter readings onto vendor device models.
    /// </summary>
    public static class WindowsAdapterMapper
    {
        /// <summary>
        /// Utilization from engine readings: the busiest of the 3D and compute engines, matching Task Manager.
        /// </summary>
        /// <param name="reading">Reading.</param>
        /// <returns>Utilization percent, or null when no engine values are available.</returns>
        public static double? Utilization(WindowsGpuAdapterReading reading)
        {
            double? best = null;
            foreach (string engine in new string[] { "3d", "compute" })
            {
                if (reading.EngineUtilization.TryGetValue(engine, out double value))
                {
                    best = best.HasValue ? Math.Max(best.Value, value) : value;
                }
            }

            if (!best.HasValue && reading.EngineUtilization.Count > 0)
            {
                foreach (double value in reading.EngineUtilization.Values)
                {
                    best = best.HasValue ? Math.Max(best.Value, value) : value;
                }
            }

            return best.HasValue ? Math.Clamp(best.Value, 0D, 100D) : null;
        }

        /// <summary>
        /// Apply common fields shared by every vendor.
        /// </summary>
        /// <param name="reading">Reading.</param>
        /// <param name="device">Target device.</param>
        /// <param name="unified">Whether the device uses shared memory as its primary memory.</param>
        public static void ApplyCommon(WindowsGpuAdapterReading reading, GpuDeviceTelemetry device, bool unified)
        {
            device.DeviceIndex = reading.Index;
            device.Source = GpuTelemetrySourceEnum.WindowsGraphicsKernel;
            device.Uuid = reading.Luid;
            device.BusId = reading.Luid;
            device.Model = String.IsNullOrWhiteSpace(reading.Description) ? "GPU" : reading.Description.Trim();
            device.DriverVersion = reading.DriverVersion;
            device.UnifiedMemory = unified;

            double? utilization = Utilization(reading);
            if (utilization.HasValue)
            {
                device.Metrics.GpuUtilizationPercent = utilization.Value;
            }

            double usedBytes = (reading.LocalUsageBytes ?? 0D) + (unified ? (reading.NonLocalUsageBytes ?? 0D) : 0D);
            double totalBytes = unified
                ? (reading.LocalBudgetBytes ?? reading.DedicatedVideoMemoryBytes) + (reading.NonLocalBudgetBytes ?? reading.SharedSystemMemoryBytes)
                : (reading.DedicatedVideoMemoryBytes > 0D ? reading.DedicatedVideoMemoryBytes : (reading.LocalBudgetBytes ?? 0D));

            if (totalBytes > 0D)
            {
                device.Metrics.MemoryTotalMegabytes = GpuMetricMath.BytesToMegabytes(totalBytes);
                device.Metrics.MemoryUsedMegabytes = GpuMetricMath.BytesToMegabytes(usedBytes);
                device.Metrics.MemoryFreeMegabytes = Math.Max(0D, device.Metrics.MemoryTotalMegabytes - device.Metrics.MemoryUsedMegabytes);
                device.Metrics.MemorySource = unified ? "sharedSystemMemory" : "dedicatedVideoMemory";
                device.Metrics.MemoryShared = unified;
            }

            device.Metrics.TemperatureCelsius = reading.TemperatureCelsius;
            device.Metrics.SmClockMHz = reading.CurrentFrequencyMHz;
            device.Metrics.MemoryClockMHz = reading.MemoryFrequencyMHz;
        }

        /// <summary>
        /// Format a LUID the way PDH instance names do.
        /// </summary>
        /// <param name="highPart">High part.</param>
        /// <param name="lowPart">Low part.</param>
        /// <returns>LUID string.</returns>
        public static string FormatLuid(int highPart, uint lowPart)
        {
            return String.Format(CultureInfo.InvariantCulture, "0x{0:X8}_0x{1:X8}", highPart, lowPart);
        }
    }
}
