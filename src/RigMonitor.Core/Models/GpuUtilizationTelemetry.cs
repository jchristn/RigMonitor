namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// GPU utilization metrics.
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
        /// Used framebuffer memory in megabytes.
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
        /// Free framebuffer memory in megabytes.
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
        /// GPU temperature in Celsius.
        /// </summary>
        public double TemperatureCelsius
        {
            get
            {
                return _TemperatureCelsius;
            }
            set
            {
                _TemperatureCelsius = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Power draw in watts.
        /// </summary>
        public double PowerUsageWatts
        {
            get
            {
                return _PowerUsageWatts;
            }
            set
            {
                _PowerUsageWatts = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// SM clock in MHz.
        /// </summary>
        public double SmClockMHz
        {
            get
            {
                return _SmClockMHz;
            }
            set
            {
                _SmClockMHz = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Memory clock in MHz.
        /// </summary>
        public double MemoryClockMHz
        {
            get
            {
                return _MemoryClockMHz;
            }
            set
            {
                _MemoryClockMHz = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// XID errors observed on the device.
        /// </summary>
        public long XidErrors
        {
            get
            {
                return _XidErrors;
            }
            set
            {
                _XidErrors = Math.Max(0L, value);
            }
        }

        private double _GpuUtilizationPercent = 0D;
        private double _MemoryUsedMegabytes = 0D;
        private double _MemoryFreeMegabytes = 0D;
        private double _TemperatureCelsius = 0D;
        private double _PowerUsageWatts = 0D;
        private double _SmClockMHz = 0D;
        private double _MemoryClockMHz = 0D;
        private long _XidErrors = 0L;
    }
}
