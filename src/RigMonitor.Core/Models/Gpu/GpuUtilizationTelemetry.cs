namespace RigMonitor.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// GPU metrics common to every vendor. Optional metrics are null when the source cannot provide them.
    /// </summary>
    public class GpuUtilizationTelemetry
    {
        /// <summary>
        /// GPU utilization percentage.
        /// </summary>
        public double GpuUtilizationPercent
        {
            get
            {
                return _GpuUtilizationPercent;
            }
            set
            {
                _GpuUtilizationPercent = Math.Clamp(value, 0D, 100D);
            }
        }

        /// <summary>
        /// Used GPU memory in megabytes.
        /// </summary>
        public double MemoryUsedMegabytes
        {
            get
            {
                return _MemoryUsedMegabytes;
            }
            set
            {
                _MemoryUsedMegabytes = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Free GPU memory in megabytes.
        /// </summary>
        public double MemoryFreeMegabytes
        {
            get
            {
                return _MemoryFreeMegabytes;
            }
            set
            {
                _MemoryFreeMegabytes = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Total GPU memory in megabytes.
        /// </summary>
        public double MemoryTotalMegabytes
        {
            get
            {
                if (_MemoryTotalMegabytes > 0D)
                {
                    return _MemoryTotalMegabytes;
                }

                return MemoryUsedMegabytes + MemoryFreeMegabytes;
            }
            set
            {
                _MemoryTotalMegabytes = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Whether a total was set explicitly rather than derived from used plus free.
        /// </summary>
        [JsonIgnore]
        public bool HasReportedMemoryTotal
        {
            get
            {
                return _MemoryTotalMegabytes > 0D || _MemoryFreeMegabytes > 0D;
            }
        }

        /// <summary>
        /// GPU memory utilization percentage.
        /// </summary>
        public double MemoryUtilizationPercent
        {
            get
            {
                double totalMegabytes = MemoryTotalMegabytes;
                if (totalMegabytes <= 0D)
                {
                    return 0D;
                }

                return Math.Clamp((MemoryUsedMegabytes / totalMegabytes) * 100D, 0D, 100D);
            }
        }

        /// <summary>
        /// Source of the reported GPU memory values.
        /// </summary>
        public string? MemorySource { get; set; } = null;

        /// <summary>
        /// Whether reported GPU memory is shared with system memory.
        /// </summary>
        public bool? MemoryShared { get; set; } = null;

        /// <summary>
        /// GPU temperature in Celsius, or null when not reported.
        /// </summary>
        public double? TemperatureCelsius
        {
            get
            {
                return _TemperatureCelsius;
            }
            set
            {
                _TemperatureCelsius = GpuMetricMath.NonNegative(value);
            }
        }

        /// <summary>
        /// Power draw in watts, or null when not reported.
        /// </summary>
        public double? PowerUsageWatts
        {
            get
            {
                return _PowerUsageWatts;
            }
            set
            {
                _PowerUsageWatts = GpuMetricMath.NonNegative(value);
            }
        }

        /// <summary>
        /// Primary compute clock in MHz (NVIDIA SM clock, AMD sclk, Apple and Qualcomm active frequency), or null.
        /// </summary>
        public double? SmClockMHz
        {
            get
            {
                return _SmClockMHz;
            }
            set
            {
                _SmClockMHz = GpuMetricMath.NonNegative(value);
            }
        }

        /// <summary>
        /// Memory clock in MHz, or null when not reported.
        /// </summary>
        public double? MemoryClockMHz
        {
            get
            {
                return _MemoryClockMHz;
            }
            set
            {
                _MemoryClockMHz = GpuMetricMath.NonNegative(value);
            }
        }

        private double _GpuUtilizationPercent = 0D;
        private double _MemoryUsedMegabytes = 0D;
        private double _MemoryFreeMegabytes = 0D;
        private double _MemoryTotalMegabytes = 0D;
        private double? _TemperatureCelsius = null;
        private double? _PowerUsageWatts = null;
        private double? _SmClockMHz = null;
        private double? _MemoryClockMHz = null;
    }
}
