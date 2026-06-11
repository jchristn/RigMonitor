namespace RigMonitor.Core.Settings
{
    using System;
    using RigMonitor.Core.Database;

    /// <summary>
    /// Telemetry persistence settings.
    /// </summary>
    public class PersistenceSettings
    {
        /// <summary>
        /// Whether telemetry persistence is enabled. Default is true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Hostname stored with persisted samples. Null or empty values resolve to localhost.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                _Hostname = String.IsNullOrWhiteSpace(value) ? "localhost" : value.Trim();
            }
        }

        /// <summary>
        /// Background collection interval in milliseconds. Minimum 1000, maximum 86400000. Default is 15000.
        /// </summary>
        public int CollectionIntervalMs
        {
            get
            {
                return _CollectionIntervalMs;
            }
            set
            {
                _CollectionIntervalMs = Math.Clamp(value, 1000, 86400000);
            }
        }

        /// <summary>
        /// Number of days to retain telemetry records. Minimum 1, maximum 3650. Default is 30.
        /// </summary>
        public int RetentionDays
        {
            get
            {
                return _RetentionDays;
            }
            set
            {
                _RetentionDays = Math.Clamp(value, 1, 3650);
            }
        }

        /// <summary>
        /// Retention pruning interval in minutes. Minimum 1, maximum 1440. Default is 60.
        /// </summary>
        public int PruneIntervalMinutes
        {
            get
            {
                return _PruneIntervalMinutes;
            }
            set
            {
                _PruneIntervalMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>
        /// Persistence database settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                _Database = value ?? throw new ArgumentNullException(nameof(Database));
            }
        }

        private string _Hostname = "localhost";
        private int _CollectionIntervalMs = 15000;
        private int _RetentionDays = 30;
        private int _PruneIntervalMinutes = 60;
        private DatabaseSettings _Database = new DatabaseSettings();
    }
}
