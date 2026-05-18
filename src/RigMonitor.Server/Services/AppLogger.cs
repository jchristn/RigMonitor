namespace RigMonitor.Server.Services
{
    using System;
    using System.IO;
    using RigMonitor.Core;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Thin wrapper around SyslogLogging.
    /// </summary>
    public class AppLogger
    {
        private readonly LoggingModule _Module;

        /// <summary>
        /// Instantiate the logger.
        /// </summary>
        /// <param name="settings">Logging settings.</param>
        public AppLogger(RigMonitor.Core.Settings.LoggingSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string logDirectory = Path.GetFullPath(settings.LogDirectory);
            Directory.CreateDirectory(logDirectory);

            _Module = new LoggingModule();
            _Module.Settings.ApplicationName = Constants.ProductName;
            _Module.Settings.EnableConsole = settings.ConsoleLogging;
            _Module.Settings.EnableColors = settings.EnableColors;
            _Module.Settings.MinimumSeverity = MapSeverity(settings.MinimumSeverity);
            _Module.Settings.LogFilename = Path.Combine(logDirectory, settings.LogFilename);
            _Module.Settings.FileLogging = settings.FileLogging
                ? (settings.IncludeDateInFilename ? FileLoggingMode.FileWithDate : FileLoggingMode.SingleLogFile)
                : FileLoggingMode.Disabled;

            foreach (SyslogServerSettings server in settings.Servers)
            {
                if (!String.IsNullOrWhiteSpace(server.Hostname))
                {
                    _Module.Servers.Add(new SyslogServer(server.Hostname, server.Port));
                }
            }
        }

        /// <summary>
        /// Write a debug log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Debug(string message)
        {
            _Module.Debug(message);
        }

        /// <summary>
        /// Write an informational log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Info(string message)
        {
            _Module.Info(message);
        }

        /// <summary>
        /// Write a warning log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Warn(string message)
        {
            _Module.Warn(message);
        }

        /// <summary>
        /// Write an error log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Error(string message)
        {
            _Module.Error(message);
        }

        /// <summary>
        /// Write an error log entry with exception details.
        /// </summary>
        /// <param name="message">Log message.</param>
        /// <param name="exception">Associated exception.</param>
        public void Error(string message, Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            _Module.Error(message + Environment.NewLine + exception);
        }

        private static Severity MapSeverity(LogSeverityEnum severity)
        {
            return severity switch
            {
                LogSeverityEnum.Debug => Severity.Debug,
                LogSeverityEnum.Warn => Severity.Warn,
                LogSeverityEnum.Error => Severity.Error,
                _ => Severity.Info
            };
        }
    }
}
