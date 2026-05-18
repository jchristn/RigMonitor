namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// Root application settings.
    /// </summary>
    public class RigMonitorSettings
    {
        /// <summary>
        /// Settings file creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Webserver configuration.
        /// </summary>
        public WebserverSettings Webserver { get; set; } = new WebserverSettings();

        /// <summary>
        /// Telemetry configuration.
        /// </summary>
        public TelemetrySettings Telemetry { get; set; } = new TelemetrySettings();

        /// <summary>
        /// Dashboard configuration.
        /// </summary>
        public DashboardSettings Dashboard { get; set; } = new DashboardSettings();

        /// <summary>
        /// Logging configuration.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();
    }
}
