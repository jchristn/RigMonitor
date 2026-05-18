namespace RigMonitor.Core.Settings
{
    using System.Collections.Generic;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Syslog targets.
        /// </summary>
        public List<SyslogServerSettings> Servers { get; set; } = new List<SyslogServerSettings>();

        /// <summary>
        /// Log directory.
        /// </summary>
        public string LogDirectory { get; set; } = "data/logs";

        /// <summary>
        /// Base log filename.
        /// </summary>
        public string LogFilename { get; set; } = "rigmonitor.log";

        /// <summary>
        /// Whether file logging is enabled.
        /// </summary>
        public bool FileLogging { get; set; } = true;

        /// <summary>
        /// Whether the log filename should include the current date.
        /// </summary>
        public bool IncludeDateInFilename { get; set; } = true;

        /// <summary>
        /// Whether console logging is enabled.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Whether ANSI console colors are enabled.
        /// </summary>
        public bool EnableColors { get; set; } = true;

        /// <summary>
        /// Minimum log severity.
        /// </summary>
        public LogSeverityEnum MinimumSeverity { get; set; } = LogSeverityEnum.Info;
    }
}
