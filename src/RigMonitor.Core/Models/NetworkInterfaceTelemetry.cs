namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Net.NetworkInformation;

    /// <summary>
    /// Single network interface telemetry.
    /// </summary>
    public class NetworkInterfaceTelemetry
    {
        /// <summary>
        /// Interface name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Interface description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Interface type.
        /// </summary>
        public NetworkInterfaceType Type { get; set; } = NetworkInterfaceType.Unknown;

        /// <summary>
        /// Operational status.
        /// </summary>
        public OperationalStatus OperationalStatus { get; set; } = OperationalStatus.Unknown;

        /// <summary>
        /// MAC address.
        /// </summary>
        public string MacAddress { get; set; } = String.Empty;

        /// <summary>
        /// Unicast IP addresses.
        /// </summary>
        public List<string> UnicastAddresses { get; set; } = new List<string>();

        /// <summary>
        /// Receive throughput in bytes per second.
        /// </summary>
        public double ReceiveBytesPerSecond
        {
            get
            {
                return _ReceiveBytesPerSecond;
            }
            set
            {
                _ReceiveBytesPerSecond = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Transmit throughput in bytes per second.
        /// </summary>
        public double TransmitBytesPerSecond
        {
            get
            {
                return _TransmitBytesPerSecond;
            }
            set
            {
                _TransmitBytesPerSecond = Math.Max(0D, value);
            }
        }

        /// <summary>
        /// Cumulative received bytes.
        /// </summary>
        public long BytesReceivedTotal
        {
            get
            {
                return _BytesReceivedTotal;
            }
            set
            {
                _BytesReceivedTotal = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Cumulative transmitted bytes.
        /// </summary>
        public long BytesSentTotal
        {
            get
            {
                return _BytesSentTotal;
            }
            set
            {
                _BytesSentTotal = Math.Max(0L, value);
            }
        }

        private double _ReceiveBytesPerSecond = 0D;
        private double _TransmitBytesPerSecond = 0D;
        private long _BytesReceivedTotal = 0L;
        private long _BytesSentTotal = 0L;
    }
}
