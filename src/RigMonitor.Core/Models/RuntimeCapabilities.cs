namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Runtime feature capabilities.
    /// </summary>
    public class RuntimeCapabilities
    {
        /// <summary>
        /// Time the capabilities were last refreshed.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Host platform.
        /// </summary>
        public HostPlatformEnum HostPlatform { get; set; } = HostPlatformEnum.Unknown;

        /// <summary>
        /// Whether the dashboard is enabled.
        /// </summary>
        public bool DashboardEnabled { get; set; } = true;

        /// <summary>
        /// Whether telemetry warmup completed.
        /// </summary>
        public bool TelemetryWarm { get; set; } = false;

        /// <summary>
        /// Whether NVIDIA telemetry is available through DCGM.
        /// </summary>
        public bool NvidiaAvailable { get; set; } = false;

        /// <summary>
        /// Whether Ollama is reachable.
        /// </summary>
        public bool OllamaAvailable { get; set; } = false;

        /// <summary>
        /// Whether vLLM telemetry collection is enabled in configuration.
        /// </summary>
        public bool VllmEnabled { get; set; } = false;

        /// <summary>
        /// Whether vLLM is reachable.
        /// </summary>
        public bool VllmAvailable { get; set; } = false;

        /// <summary>
        /// Whether Utilyze telemetry collection is enabled in configuration.
        /// </summary>
        public bool UtilyzeEnabled { get; set; } = false;

        /// <summary>
        /// Whether Utilyze is reachable.
        /// </summary>
        public bool UtilyzeAvailable { get; set; } = false;

        /// <summary>
        /// Configured DCGM exporter endpoint.
        /// </summary>
        public string DcgmExporterUrl { get; set; } = String.Empty;

        /// <summary>
        /// Configured Ollama base URL.
        /// </summary>
        public string OllamaBaseUrl { get; set; } = String.Empty;

        /// <summary>
        /// Configured vLLM metrics endpoint.
        /// </summary>
        public string VllmMetricsUrl { get; set; } = String.Empty;

        /// <summary>
        /// Configured Utilyze live endpoint.
        /// </summary>
        public string UtilyzeLiveUrl { get; set; } = String.Empty;
    }
}
