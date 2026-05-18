namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Aggregate network telemetry.
    /// </summary>
    public class NetworkTelemetry
    {
        /// <summary>
        /// Aggregate receive throughput in bytes per second.
        /// </summary>
        public double TotalReceiveBytesPerSecond
        {
            get
            {
                return _TotalReceiveBytesPerSecond;
            }
            set
            {
                _TotalReceiveBytesPerSecond = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Aggregate transmit throughput in bytes per second.
        /// </summary>
        public double TotalTransmitBytesPerSecond
        {
            get
            {
                return _TotalTransmitBytesPerSecond;
            }
            set
            {
                _TotalTransmitBytesPerSecond = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Number of operational interfaces.
        /// </summary>
        public int ActiveInterfaceCount
        {
            get
            {
                return _ActiveInterfaceCount;
            }
            set
            {
                _ActiveInterfaceCount = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Interface snapshots.
        /// </summary>
        public List<NetworkInterfaceTelemetry> Interfaces { get; set; } = new List<NetworkInterfaceTelemetry>();

        private double _TotalReceiveBytesPerSecond = 0D;
        private double _TotalTransmitBytesPerSecond = 0D;
        private int _ActiveInterfaceCount = 0;
    }
}
