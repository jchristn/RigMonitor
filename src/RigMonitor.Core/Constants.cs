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
    }
}
