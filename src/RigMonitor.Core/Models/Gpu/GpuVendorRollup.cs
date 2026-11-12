namespace RigMonitor.Core.Models
{
    /// <summary>
    /// GPU roll-up values for one vendor within one bucket.
    /// Device values are aggregated per sample first (utilization and temperature averaged, memory and power summed), then averaged across the bucket.
    /// </summary>
    public class GpuVendorRollup
    {
        /// <summary>
        /// Samples in the bucket with at least one device from this vendor.
        /// </summary>
        public long SampleCount { get; set; } = 0L;

        /// <summary>
        /// Average device count.
        /// </summary>
        public double? AverageDeviceCount { get; set; } = null;

        /// <summary>
        /// Average utilization percentage.
        /// </summary>
        public double? AverageUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Minimum per-sample utilization percentage.
        /// </summary>
        public double? MinUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Maximum per-sample utilization percentage.
        /// </summary>
        public double? MaxUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average memory utilization percentage.
        /// </summary>
        public double? AverageMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average memory used in megabytes (summed across devices per sample).
        /// </summary>
        public double? AverageMemoryUsedMegabytes { get; set; } = null;

        /// <summary>
        /// Average memory total in megabytes (summed across devices per sample).
        /// </summary>
        public double? AverageMemoryTotalMegabytes { get; set; } = null;

        /// <summary>
        /// Average temperature in Celsius.
        /// </summary>
        public double? AverageTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Minimum per-sample temperature in Celsius.
        /// </summary>
        public double? MinTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Maximum per-sample temperature in Celsius.
        /// </summary>
        public double? MaxTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Average total power in watts (summed across devices per sample).
        /// </summary>
        public double? AveragePowerUsageWatts { get; set; } = null;

        /// <summary>
        /// Average hotspot temperature in Celsius (AMD).
        /// </summary>
        public double? AverageHotspotTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Average GTT used in megabytes (AMD).
        /// </summary>
        public double? AverageGttUsedMegabytes { get; set; } = null;

        /// <summary>
        /// Average recommended working set in megabytes (Apple).
        /// </summary>
        public double? AverageWorkingSetMegabytes { get; set; } = null;

        /// <summary>
        /// Highest thermal state observed, 0 (nominal) to 3 (critical) (Apple).
        /// </summary>
        public double? MaxThermalState { get; set; } = null;

        /// <summary>
        /// Average memory budget in megabytes (Qualcomm).
        /// </summary>
        public double? AverageMemoryBudgetMegabytes { get; set; } = null;
    }
}
