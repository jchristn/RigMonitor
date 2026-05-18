namespace RigMonitor.Core.Models
{
    using System;
    using System.IO;

    /// <summary>
    /// Single disk volume telemetry.
    /// </summary>
    public class DiskVolumeTelemetry
    {
        /// <summary>
        /// Volume name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Mount point or root path.
        /// </summary>
        public string MountPoint { get; set; } = String.Empty;

        /// <summary>
        /// Drive type string.
        /// </summary>
        public DriveType DriveType { get; set; } = DriveType.Unknown;

        /// <summary>
        /// Filesystem type.
        /// </summary>
        public string FileSystem { get; set; } = String.Empty;

        /// <summary>
        /// Total capacity in bytes.
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
        /// Free capacity in bytes.
        /// </summary>
        public long FreeBytes
        {
            get
            {
                return _FreeBytes;
            }
            set
            {
                _FreeBytes = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Used capacity in bytes.
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
        /// Utilization percentage from 0 to 100.
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
        private long _FreeBytes = 0L;
        private long _UsedBytes = 0L;
        private double _UtilizationPercent = 0D;
    }
}
