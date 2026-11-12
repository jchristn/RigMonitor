namespace RigMonitor.Core
{
    /// <summary>
    /// Shared application constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public const string ProductName = "RigMonitor";

        /// <summary>
        /// Default settings filename.
        /// </summary>
        public const string DefaultSettingsFilename = "rigmonitor.json";

        /// <summary>
        /// Default dashboard title.
        /// </summary>
        public const string DefaultDashboardTitle = "RigMonitor Dashboard";

        /// <summary>
        /// Default DCGM exporter endpoint.
        /// </summary>
        public const string DefaultDcgmExporterUrl = "http://localhost:9400/metrics";

        /// <summary>
        /// Default AMD Device Metrics Exporter endpoint.
        /// </summary>
        public const string DefaultAmdMetricsExporterUrl = "http://localhost:5000/metrics";

        /// <summary>
        /// Default Linux DRM sysfs class root.
        /// </summary>
        public const string DefaultDrmSysfsRoot = "/sys/class/drm";

        /// <summary>
        /// Default Linux sysfs root.
        /// </summary>
        public const string DefaultSysfsRoot = "/sys";

        /// <summary>
        /// Default Linux procfs root.
        /// </summary>
        public const string DefaultProcRoot = "/proc";

        /// <summary>
        /// Default Ollama endpoint.
        /// </summary>
        public const string DefaultOllamaBaseUrl = "http://localhost:11434";

        /// <summary>
        /// Default vLLM Prometheus metrics endpoint.
        /// </summary>
        public const string DefaultVllmMetricsUrl = "http://localhost:8000/metrics";

        /// <summary>
        /// Default Utilyze live telemetry endpoint.
        /// </summary>
        public const string DefaultUtilyzeLiveUrl = "ws://127.0.0.1:8079/live";

        /// <summary>
        /// Default SQLite telemetry history database filename.
        /// </summary>
        public const string DefaultTelemetryHistoryDatabaseFilename = "data/rigmonitor.telemetry.db";

        /// <summary>
        /// Total generated identifier length including prefix.
        /// </summary>
        public const int IdentifierLength = 32;

        /// <summary>
        /// Telemetry sample identifier prefix.
        /// </summary>
        public const string TelemetrySampleIdentifierPrefix = "tel_";

        /// <summary>
        /// Telemetry GPU sample identifier prefix.
        /// </summary>
        public const string TelemetryGpuSampleIdentifierPrefix = "tgd_";
    }
}
