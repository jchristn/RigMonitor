namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Bucketized telemetry roll-up result.
    /// </summary>
    public class TelemetryRollupResult
    {
        /// <summary>
        /// Roll-up start time in UTC.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow.AddHours(-1);

        /// <summary>
        /// Roll-up end time in UTC.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket interval in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 60;

        /// <summary>
        /// Total matching samples.
        /// </summary>
        public long TotalSamples { get; set; } = 0L;

        /// <summary>
        /// Aggregate buckets.
        /// </summary>
        public List<TelemetryRollupBucket> Buckets
        {
            get
            {
                return _Buckets;
            }
            set
            {
                _Buckets = value ?? new List<TelemetryRollupBucket>();
            }
        }

        private List<TelemetryRollupBucket> _Buckets = new List<TelemetryRollupBucket>();
    }
}
