namespace RigMonitor.Server.Services
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;
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
        private ILogger? _Sink = null;

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
            Forward(LogLevel.Debug, message, null);
        }

        /// <summary>
        /// Write an informational log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Info(string message)
        {
            _Module.Info(message);
            Forward(LogLevel.Information, message, null);
        }

        /// <summary>
        /// Write a warning log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Warn(string message)
        {
            _Module.Warn(message);
            Forward(LogLevel.Warning, message, null);
        }

        /// <summary>
        /// Write an error log entry.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Error(string message)
        {
            _Module.Error(message);
            Forward(LogLevel.Error, message, null);
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
            Forward(LogLevel.Error, message, exception);
        }

        /// <summary>
        /// Attach a structured log sink (for example the OTLP/Loki pipeline). Every subsequent message is also
        /// written to the sink, stamped with the current trace and span when one is active.
        /// Sink failures are ignored. Pass null to detach.
        /// </summary>
        /// <param name="sink">Sink logger, or null.</param>
        public void AttachSink(ILogger? sink)
        {
            _Sink = sink;
        }

        private void Forward(LogLevel level, string message, Exception? exception)
        {
            ILogger? sink = _Sink;
            if (sink == null) return;
            try
            {
                if (!sink.IsEnabled(level)) return;
                sink.Log(level, exception, "{Message}", message);
            }
            catch (Exception)
            {
            }
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
