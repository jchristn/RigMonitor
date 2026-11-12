namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Request for bucketized telemetry roll-up data.
    /// </summary>
    public class TelemetryRollupRequest
    {
        /// <summary>
        /// Optional hostname filter.
        /// </summary>
        public string? Hostname { get; set; } = null;

        /// <summary>
        /// Inclusive roll-up start time.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow.AddHours(-1);

        /// <summary>
        /// Exclusive roll-up end time.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket interval in minutes. Minimum 1, maximum 1440. Default is 60.
        /// </summary>
        public int BucketMinutes
        {
            get
            {
                return _BucketMinutes;
            }
            set
            {
                _BucketMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>
        /// Optional GPU UUID filter.
        /// </summary>
        public string? GpuUuid { get; set; } = null;

        /// <summary>
        /// Optional GPU vendor filter. When set, bucket GPU averages are computed from that vendor's devices only.
        /// </summary>
        public GpuVendorEnum? GpuVendor { get; set; } = null;

        /// <summary>
        /// Whether each bucket includes a per-vendor GPU breakdown.
        /// </summary>
        public bool IncludeGpuVendorBreakdown { get; set; } = true;

        /// <summary>
        /// Whether empty buckets should be included. Default is true.
        /// </summary>
        public bool IncludeEmptyBuckets { get; set; } = true;

        private int _BucketMinutes = 60;
    }
}
