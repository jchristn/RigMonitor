namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Aggregate disk telemetry.
    /// </summary>
    public class DiskTelemetry
    {
        /// <summary>
        /// Estimated disk reads per second.
        /// </summary>
        public double ReadOperationsPerSecond
        {
            get
            {
                return _ReadOperationsPerSecond;
            }
            set
            {
                _ReadOperationsPerSecond = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Estimated disk writes per second.
        /// </summary>
        public double WriteOperationsPerSecond
        {
            get
            {
                return _WriteOperationsPerSecond;
            }
            set
            {
                _WriteOperationsPerSecond = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Estimated read queue depth.
        /// </summary>
        public double ReadQueueDepth
        {
            get
            {
                return _ReadQueueDepth;
            }
            set
            {
                _ReadQueueDepth = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Estimated write queue depth.
        /// </summary>
        public double WriteQueueDepth
        {
            get
            {
                return _WriteQueueDepth;
            }
            set
            {
                _WriteQueueDepth = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Mounted volumes visible to the host.
        /// </summary>
        public List<DiskVolumeTelemetry> Volumes { get; set; } = new List<DiskVolumeTelemetry>();

        private double _ReadOperationsPerSecond = 0D;
        private double _WriteOperationsPerSecond = 0D;
        private double _ReadQueueDepth = 0D;
        private double _WriteQueueDepth = 0D;
    }
}
