namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Telemetry persistence worker status.
    /// </summary>
    public class TelemetryPersistenceStatus
    {
        /// <summary>
        /// Whether persistence is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Configured hostname written to persisted samples.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Configured collection interval.
        /// </summary>
        public int CollectionIntervalMs { get; set; } = 60000;

        /// <summary>
        /// Configured retention in days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Database provider type.
        /// </summary>
        public string DatabaseType { get; set; } = "Sqlite";

        /// <summary>
        /// Database filename.
        /// </summary>
        public string DatabaseFilename { get; set; } = String.Empty;

        /// <summary>
        /// Last collection attempt time.
        /// </summary>
        public DateTime? LastAttemptUtc { get; set; } = null;

        /// <summary>
        /// Last successful collection time.
        /// </summary>
        public DateTime? LastSuccessUtc { get; set; } = null;

        /// <summary>
        /// Next scheduled collection time.
        /// </summary>
        public DateTime? NextCollectionUtc { get; set; } = null;

        /// <summary>
        /// Last persistence error.
        /// </summary>
        public string? LastError { get; set; } = null;
    }
}
