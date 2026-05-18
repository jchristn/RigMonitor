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
        /// Configured DCGM exporter endpoint.
        /// </summary>
        public string DcgmExporterUrl { get; set; } = String.Empty;

        /// <summary>
        /// Configured Ollama base URL.
        /// </summary>
        public string OllamaBaseUrl { get; set; } = String.Empty;
    }
}
