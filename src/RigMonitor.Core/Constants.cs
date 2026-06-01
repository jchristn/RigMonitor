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
    }
}
