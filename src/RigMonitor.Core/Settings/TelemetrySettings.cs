namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// Telemetry subsystem settings.
    /// </summary>
    public class TelemetrySettings
    {
        /// <summary>
        /// DCGM exporter metrics URL.
        /// </summary>
        public string DcgmExporterUrl { get; set; } = Constants.DefaultDcgmExporterUrl;

        /// <summary>
        /// Ollama base URL.
        /// </summary>
        public string OllamaBaseUrl { get; set; } = Constants.DefaultOllamaBaseUrl;

        /// <summary>
        /// Outbound request timeout in milliseconds.
        /// Minimum 500, maximum 300000.
        /// </summary>
        public int RequestTimeoutMs
        {
            get
            {
                return _RequestTimeoutMs;
            }
            set
            {
                _RequestTimeoutMs = Math.Clamp(value, 500, 300000);
            }
        }

        /// <summary>
        /// Warmup delay in milliseconds.
        /// Minimum 0, maximum 60000.
        /// </summary>
        public int WarmupDelayMs
        {
            get
            {
                return _WarmupDelayMs;
            }
            set
            {
                _WarmupDelayMs = Math.Clamp(value, 0, 60000);
            }
        }

        private int _RequestTimeoutMs = 5000;
        private int _WarmupDelayMs = 1000;
    }
}
