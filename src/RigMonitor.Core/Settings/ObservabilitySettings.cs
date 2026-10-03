namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// Settings for exporting RigMonitor's own metrics, traces, and logs (OTLP, Prometheus scrape endpoint, Loki).
    /// These govern observability of the RigMonitor process itself, not the host telemetry it collects.
    /// Each value can be overridden at startup with a RIGMONITOR_OBSERVABILITY_* environment variable; see TELEMETRY.md.
    /// </summary>
    public class ObservabilitySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for exporting RigMonitor's own telemetry. Default is true.
        /// When false, no exporter host is started; the in-process instrumentation remains a no-op.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Service name reported as service.name on metrics, traces, and logs. Default is rigmonitor.
        /// </summary>
        public string ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                _ServiceName = String.IsNullOrWhiteSpace(value) ? "rigmonitor" : value.Trim();
            }
        }

        /// <summary>
        /// Whether metrics and traces are pushed over OTLP. Default is true.
        /// </summary>
        public bool OtlpEnabled { get; set; } = true;

        /// <summary>
        /// OTLP collector endpoint. Use port 4317 with the grpc protocol, or port 4318 with httpprotobuf.
        /// Default is http://127.0.0.1:4317. Invalid values fall back to the default.
        /// </summary>
        public string OtlpEndpoint
        {
            get
            {
                return _OtlpEndpoint;
            }
            set
            {
                _OtlpEndpoint = IsAbsoluteUri(value) ? value.Trim() : _DefaultOtlpEndpoint;
            }
        }

        /// <summary>
        /// OTLP protocol: grpc (default) or httpprotobuf. Any other value is treated as grpc.
        /// </summary>
        public string OtlpProtocol
        {
            get
            {
                return _OtlpProtocol;
            }
            set
            {
                _OtlpProtocol = String.Equals(value, "httpprotobuf", StringComparison.OrdinalIgnoreCase) ? "httpprotobuf" : "grpc";
            }
        }

        /// <summary>
        /// Whether an in-process Prometheus scrape endpoint is served on its own port. Default is true.
        /// The endpoint has no authentication; keep it on an internal network.
        /// </summary>
        public bool PrometheusEnabled { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus scrape endpoint binds to. Default is 127.0.0.1 (loopback only).
        /// Use * to listen on all interfaces, for example inside a container.
        /// </summary>
        public string PrometheusHostname
        {
            get
            {
                return _PrometheusHostname;
            }
            set
            {
                _PrometheusHostname = String.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
            }
        }

        /// <summary>
        /// Prometheus scrape endpoint port. Default is 9464. Minimum 1, maximum 65535.
        /// Must differ from the webserver port.
        /// </summary>
        public int PrometheusPort
        {
            get
            {
                return _PrometheusPort;
            }
            set
            {
                _PrometheusPort = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// Prometheus scrape endpoint path. Default is /metrics.
        /// </summary>
        public string PrometheusPath
        {
            get
            {
                return _PrometheusPath;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    _PrometheusPath = "/metrics";
                    return;
                }

                string trimmed = value.Trim();
                _PrometheusPath = trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
            }
        }

        /// <summary>
        /// Whether logs are pushed directly to a Loki 3.x OTLP endpoint. Default is false.
        /// </summary>
        public bool LokiEnabled { get; set; } = false;

        /// <summary>
        /// Loki OTLP base endpoint (the /v1/logs suffix is appended). Default is http://127.0.0.1:3100/otlp.
        /// Invalid values fall back to the default.
        /// </summary>
        public string LokiEndpoint
        {
            get
            {
                return _LokiEndpoint;
            }
            set
            {
                _LokiEndpoint = IsAbsoluteUri(value) ? value.Trim() : _DefaultLokiEndpoint;
            }
        }

        /// <summary>
        /// Optional Loki tenant, sent as X-Scope-OrgID. Default is null (single-tenant Loki).
        /// </summary>
        public string? LokiTenantId { get; set; } = null;

        /// <summary>
        /// Parent-based trace sampling ratio. Default is 1.0 (sample everything). Minimum 0, maximum 1.
        /// </summary>
        public double TraceSamplingRatio
        {
            get
            {
                return _TraceSamplingRatio;
            }
            set
            {
                _TraceSamplingRatio = Double.IsNaN(value) ? 1D : Math.Clamp(value, 0D, 1D);
            }
        }

        /// <summary>
        /// OTLP metric export interval in milliseconds. Default is 15000. Minimum 1000, maximum 300000.
        /// </summary>
        public int MetricExportIntervalMs
        {
            get
            {
                return _MetricExportIntervalMs;
            }
            set
            {
                _MetricExportIntervalMs = Math.Clamp(value, 1000, 300000);
            }
        }

        /// <summary>
        /// Whether .NET runtime and process metrics (GC, thread pool, CPU, memory) are exported. Default is true.
        /// </summary>
        public bool IncludeRuntimeMetrics { get; set; } = true;

        #endregion

        #region Private-Members

        private const string _DefaultOtlpEndpoint = "http://127.0.0.1:4317";
        private const string _DefaultLokiEndpoint = "http://127.0.0.1:3100/otlp";

        private string _ServiceName = "rigmonitor";
        private string _OtlpEndpoint = _DefaultOtlpEndpoint;
        private string _OtlpProtocol = "grpc";
        private string _PrometheusHostname = "127.0.0.1";
        private int _PrometheusPort = 9464;
        private string _PrometheusPath = "/metrics";
        private string _LokiEndpoint = _DefaultLokiEndpoint;
        private double _TraceSamplingRatio = 1D;
        private int _MetricExportIntervalMs = 15000;

        #endregion

        #region Private-Methods

        private static bool IsAbsoluteUri(string? value)
        {
            return !String.IsNullOrWhiteSpace(value) && Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? _);
        }

        #endregion
    }
}
