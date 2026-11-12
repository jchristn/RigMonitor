namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
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
        /// Dashboard default for hiding GPU vendor sections the host does not support.
        /// </summary>
        public bool HideUnsupportedGpuSections { get; set; } = false;

        /// <summary>
        /// Whether NVIDIA telemetry is available through DCGM.
        /// </summary>
        public bool NvidiaAvailable { get; set; } = false;

        /// <summary>
        /// Whether AMD GPU telemetry is available.
        /// </summary>
        public bool AmdAvailable { get; set; } = false;

        /// <summary>
        /// Whether Apple GPU telemetry is available.
        /// </summary>
        public bool AppleGpuAvailable { get; set; } = false;

        /// <summary>
        /// Whether Qualcomm GPU telemetry is available.
        /// </summary>
        public bool QualcommAvailable { get; set; } = false;

        /// <summary>
        /// Whether any GPU vendor is available.
        /// </summary>
        public bool GpuAvailable
        {
            get
            {
                return NvidiaAvailable || AmdAvailable || AppleGpuAvailable || QualcommAvailable;
            }
        }

        /// <summary>
        /// Resolved AMD telemetry source.
        /// </summary>
        public GpuTelemetrySourceEnum? AmdSource { get; set; } = null;

        /// <summary>
        /// Resolved Qualcomm telemetry source.
        /// </summary>
        public GpuTelemetrySourceEnum? QualcommSource { get; set; } = null;

        /// <summary>
        /// Native Apple GPU features that loaded (ioKit, metal, ioReport, hidSensors, processInfo).
        /// </summary>
        public List<string> AppleFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Probe result for every GPU vendor considered on this host.
        /// </summary>
        public List<GpuProbeResult> GpuProviders { get; set; } = new List<GpuProbeResult>();

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
        /// Configured AMD Device Metrics Exporter endpoint.
        /// </summary>
        public string AmdMetricsExporterUrl { get; set; } = String.Empty;

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
