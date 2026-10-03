namespace RigMonitor.Server.Services
{
    using System;
    using Microsoft.Extensions.Logging;
    using Radiant;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Owns the process-wide Radiant telemetry host. Subscribes to the RigMonitor and Watson meters and activity
    /// sources and exports them over OTLP, an in-process Prometheus scrape endpoint, and optionally Loki.
    /// All operations are best-effort: an initialization failure logs a warning and the server runs without export.
    /// Create exactly one instance per process and dispose it on shutdown so exporters flush.
    /// Thread safety: safe for concurrent use after construction.
    /// </summary>
    public class ObservabilityService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Whether a telemetry host is running.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return _Host != null;
            }
        }

        /// <summary>
        /// The Radiant settings used to start the host, or null when observability is disabled.
        /// </summary>
        public RadiantSettings? RadiantSettings
        {
            get
            {
                return _RadiantSettings;
            }
        }

        #endregion

        #region Private-Members

        private readonly RadiantHost? _Host = null;
        private readonly RadiantSettings? _RadiantSettings = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the telemetry host from settings.
        /// </summary>
        /// <param name="settings">Observability settings. When null or disabled, no host is started.</param>
        /// <param name="logger">Application logger used to report the outcome.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public ObservabilityService(ObservabilitySettings? settings, AppLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            if (settings == null || !settings.Enabled)
            {
                logger.Info("Observability export disabled.");
                return;
            }

            try
            {
                RadiantSettings radiant = BuildRadiantSettings(settings);
                _Host = RadiantHost.Start(radiant);
                _RadiantSettings = radiant;

                logger.Info(
                    "Observability export enabled for service " + settings.ServiceName
                    + " (OTLP " + (settings.OtlpEnabled ? settings.OtlpProtocol + " " + settings.OtlpEndpoint : "off")
                    + ", Prometheus " + (settings.PrometheusEnabled ? "http://" + settings.PrometheusHostname + ":" + settings.PrometheusPort + settings.PrometheusPath : "off")
                    + ", Loki " + (settings.LokiEnabled ? settings.LokiEndpoint : "off") + ").");
            }
            catch (Exception exception)
            {
                _Host = null;
                _RadiantSettings = null;
                logger.Warn("Observability export disabled because initialization failed: " + DescribeException(exception));
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build Radiant settings from RigMonitor observability settings.
        /// </summary>
        /// <param name="settings">Observability settings.</param>
        /// <returns>Radiant settings subscribed to the RigMonitor and Watson sources.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static RadiantSettings BuildRadiantSettings(ObservabilitySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            RadiantSettings radiant = new RadiantSettings(settings.ServiceName);
            radiant.Enable = true;

            radiant.Sources.AddMeter(RigMonitorTelemetryNames.MeterName);
            radiant.Sources.AddActivitySource(RigMonitorTelemetryNames.ActivitySourceName);
            radiant.Sources.AddMeter(RigMonitorTelemetryNames.WatsonSourceName);
            radiant.Sources.AddActivitySource(RigMonitorTelemetryNames.WatsonSourceName);

            radiant.Metrics.ExportIntervalMs = settings.MetricExportIntervalMs;
            radiant.Metrics.IncludeRuntime = settings.IncludeRuntimeMetrics;
            radiant.Metrics.IncludeProcess = settings.IncludeRuntimeMetrics;
            radiant.Metrics.DefineAll(RigMonitorMetricCatalog.All);

            radiant.Traces.SamplingRatio = settings.TraceSamplingRatio;

            radiant.Otlp.Enable = settings.OtlpEnabled;
            radiant.Otlp.Endpoint = settings.OtlpEndpoint;
            radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpProtocolEnum.HttpProtobuf
                : OtlpProtocolEnum.Grpc;

            radiant.Prometheus.Enable = settings.PrometheusEnabled;
            radiant.Prometheus.Hostname = settings.PrometheusHostname;
            radiant.Prometheus.Port = settings.PrometheusPort;
            radiant.Prometheus.Path = settings.PrometheusPath;

            // Logs are exported only when Loki is configured; otherwise the OTLP target (typically Tempo) would
            // receive log records it does not accept.
            radiant.Logs.Enable = settings.LokiEnabled;
            radiant.Loki.Enable = settings.LokiEnabled;
            radiant.Loki.Endpoint = settings.LokiEndpoint;
            radiant.Loki.TenantId = String.IsNullOrWhiteSpace(settings.LokiTenantId) ? null : settings.LokiTenantId;

            return radiant;
        }

        /// <summary>
        /// Create a logger whose records flow through the OTLP and Loki log pipeline with trace correlation.
        /// </summary>
        /// <param name="categoryName">Logger category.</param>
        /// <returns>Logger, or null when observability is disabled or log export is unavailable.</returns>
        public ILogger? CreateLogger(string categoryName)
        {
            if (_Host == null || String.IsNullOrWhiteSpace(categoryName)) return null;
            try
            {
                return _Host.CreateLogger(categoryName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Flush exporters and stop the telemetry host. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private static string DescribeException(Exception exception)
        {
            string description = exception.Message;
            Exception? inner = exception.InnerException;
            int depth = 0;
            while (inner != null && depth < 5)
            {
                description += " -> " + inner.GetType().Name + ": " + inner.Message;
                inner = inner.InnerException;
                depth++;
            }

            return description;
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;

            if (disposing)
            {
                try
                {
                    _Host?.ForceFlush(5000);
                }
                catch (Exception)
                {
                }

                try
                {
                    _Host?.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
    }
}
