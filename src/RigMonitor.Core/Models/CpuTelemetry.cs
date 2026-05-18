namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// CPU telemetry payload.
    /// </summary>
    public class CpuTelemetry
    {
        /// <summary>
        /// Logical CPU core count.
        /// </summary>
        public int LogicalCoreCount
        {
            get
            {
                return _LogicalCoreCount;
            }
            set
            {
                _LogicalCoreCount = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Total CPU utilization percentage from 0 to 100.
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

        private int _LogicalCoreCount = Environment.ProcessorCount;
        private double _UtilizationPercent = 0D;
    }
}
