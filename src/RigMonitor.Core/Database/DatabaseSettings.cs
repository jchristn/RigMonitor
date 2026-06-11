namespace RigMonitor.Core.Database
{
    using System;

    /// <summary>
    /// Persistence database settings.
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Database provider type. Default is Sqlite.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// SQLite database filename. Default is data/rigmonitor.telemetry.db.
        /// </summary>
        public string Filename
        {
            get
            {
                return _Filename;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Filename));
                _Filename = value;
            }
        }

        /// <summary>
        /// Whether SQL statements should be logged. Default is false.
        /// </summary>
        public bool LogQueries { get; set; } = false;

        private string _Filename = Constants.DefaultTelemetryHistoryDatabaseFilename;
    }
}
