namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Memory telemetry payload.
    /// </summary>
    public class MemoryTelemetry
    {
        /// <summary>
        /// Total physical memory in bytes.
        /// </summary>
        public long TotalBytes
        {
            get
            {
                return _TotalBytes;
            }
            set
            {
                _TotalBytes = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Available physical memory in bytes.
        /// </summary>
        public long AvailableBytes
        {
            get
            {
                return _AvailableBytes;
            }
            set
            {
                _AvailableBytes = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Used physical memory in bytes.
        /// </summary>
        public long UsedBytes
        {
            get
            {
                return _UsedBytes;
            }
            set
            {
                _UsedBytes = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Memory utilization percentage from 0 to 100.
        /// </summary>
        public double UtilizationPercent
        {
            get
            {
                return _UtilizationPercent;
            }
            set
            {
                _UtilizationPercent = Math.Clamp(value, 0D, 100D);
            }
        }

        private long _TotalBytes = 0L;
        private long _AvailableBytes = 0L;
        private long _UsedBytes = 0L;
        private double _UtilizationPercent = 0D;
    }
}
