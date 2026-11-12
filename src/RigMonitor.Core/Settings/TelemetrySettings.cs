namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// Telemetry subsystem settings.
    /// </summary>
    public class TelemetrySettings
    {
        /// <summary>
        /// Whether NVIDIA telemetry (DCGM exporter) is probed and collected.
        /// </summary>
        public bool NvidiaEnabled { get; set; } = true;

        /// <summary>
        /// DCGM exporter metrics URL.
        /// </summary>
        public string DcgmExporterUrl { get; set; } = Constants.DefaultDcgmExporterUrl;

        /// <summary>
        /// Whether AMD GPU telemetry is probed and collected.
        /// </summary>
        public bool AmdEnabled { get; set; } = true;

        /// <summary>
        /// AMD telemetry source: auto, native, sysfs, or exporter. Unrecognized values fall back to auto.
        /// </summary>
        public string AmdSource
        {
            get
            {
                return _AmdSource;
            }
            set
            {
                _AmdSource = NormalizeChoice(value, _AmdSourceChoices);
            }
        }

        /// <summary>
        /// Explicit path to libamd_smi. When empty, standard ROCm locations and the loader path are searched.
        /// </summary>
        public string AmdSmiLibraryPath { get; set; } = String.Empty;

        /// <summary>
        /// Whether amdgpu sysfs fills fields the AMD SMI library cannot provide.
        /// </summary>
        public bool AmdSysfsGapFillEnabled { get; set; } = true;

        /// <summary>
        /// DRM sysfs class root used for AMD and Qualcomm discovery.
        /// </summary>
        public string AmdSysfsRoot { get; set; } = Constants.DefaultDrmSysfsRoot;

        /// <summary>
        /// AMD Device Metrics Exporter URL.
        /// </summary>
        public string AmdMetricsExporterUrl { get; set; } = Constants.DefaultAmdMetricsExporterUrl;

        /// <summary>
        /// Whether Apple GPU telemetry is probed and collected.
        /// </summary>
        public bool AppleGpuEnabled { get; set; } = true;

        /// <summary>
        /// Whether Apple GPU power and frequency are read through IOReport.
        /// </summary>
        public bool AppleIOReportEnabled { get; set; } = true;

        /// <summary>
        /// Whether Apple GPU temperatures are read through IOHID sensors.
        /// </summary>
        public bool AppleHidSensorsEnabled { get; set; } = true;

        /// <summary>
        /// Background sampling interval for Apple GPU power and frequency in milliseconds.
        /// Minimum 250, maximum 10000.
        /// </summary>
        public int AppleSamplerIntervalMs
        {
            get
            {
                return _AppleSamplerIntervalMs;
            }
            set
            {
                _AppleSamplerIntervalMs = Math.Clamp(value, 250, 10000);
            }
        }

        /// <summary>
        /// Whether Qualcomm GPU telemetry is probed and collected.
        /// </summary>
        public bool QualcommEnabled { get; set; } = true;

        /// <summary>
        /// Qualcomm telemetry source: auto, windows, drmMsm, or kgsl. Unrecognized values fall back to auto.
        /// </summary>
        public string QualcommSource
        {
            get
            {
                return _QualcommSource;
            }
            set
            {
                _QualcommSource = NormalizeChoice(value, _QualcommSourceChoices);
            }
        }

        /// <summary>
        /// Linux sysfs root used for Qualcomm devfreq, thermal, and kgsl discovery.
        /// </summary>
        public string QualcommSysfsRoot { get; set; } = Constants.DefaultSysfsRoot;

        /// <summary>
        /// Linux procfs root used for DRM fdinfo utilization.
        /// </summary>
        public string QualcommProcRoot { get; set; } = Constants.DefaultProcRoot;

        /// <summary>
        /// Whether AMD and Intel adapters seen by the Windows graphics kernel backend feed their vendor sections.
        /// </summary>
        public bool WindowsGenericGpuEnabled { get; set; } = true;

        /// <summary>
        /// Background sampling interval for Windows GPU counters in milliseconds.
        /// Minimum 250, maximum 10000.
        /// </summary>
        public int WindowsGpuSamplerIntervalMs
        {
            get
            {
                return _WindowsGpuSamplerIntervalMs;
            }
            set
            {
                _WindowsGpuSamplerIntervalMs = Math.Clamp(value, 250, 10000);
            }
        }

        /// <summary>
        /// Ollama base URL.
        /// </summary>
        public string OllamaBaseUrl { get; set; } = Constants.DefaultOllamaBaseUrl;

        /// <summary>
        /// Whether vLLM telemetry collection is enabled.
        /// </summary>
        public bool VllmEnabled { get; set; } = false;

        /// <summary>
        /// vLLM Prometheus metrics URL.
        /// </summary>
        public string VllmMetricsUrl { get; set; } = Constants.DefaultVllmMetricsUrl;

        /// <summary>
        /// Whether Utilyze telemetry collection is enabled.
        /// </summary>
        public bool UtilyzeEnabled { get; set; } = false;

        /// <summary>
        /// Utilyze live WebSocket URL.
        /// </summary>
        public string UtilyzeLiveUrl { get; set; } = Constants.DefaultUtilyzeLiveUrl;

        /// <summary>
        /// Client identifier used when connecting to the Utilyze live endpoint.
        /// </summary>
        public string UtilyzeClientId { get; set; } = "rigmonitor";

        /// <summary>
        /// Number of milliseconds after which the latest Utilyze live sample is considered stale.
        /// Minimum 1000, maximum 3600000.
        /// </summary>
        public int UtilyzeSampleStaleAfterMs
        {
            get
            {
                return _UtilyzeSampleStaleAfterMs;
            }
            set
            {
                _UtilyzeSampleStaleAfterMs = Math.Clamp(value, 1000, 3600000);
            }
        }

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

        /// <summary>
        /// Number of milliseconds after which a last successful section sample is considered stale.
        /// Minimum 1000, maximum 3600000.
        /// </summary>
        public int SectionStaleAfterMs
        {
            get
            {
                return _SectionStaleAfterMs;
            }
            set
            {
                _SectionStaleAfterMs = Math.Clamp(value, 1000, 3600000);
            }
        }

        private static string NormalizeChoice(string? value, string[] choices)
        {
            if (!String.IsNullOrWhiteSpace(value))
            {
                foreach (string choice in choices)
                {
                    if (String.Equals(choice, value.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return choice;
                    }
                }
            }

            return choices[0];
        }

        private static readonly string[] _AmdSourceChoices = new string[] { "auto", "native", "sysfs", "exporter" };
        private static readonly string[] _QualcommSourceChoices = new string[] { "auto", "windows", "drmMsm", "kgsl" };
        private string _AmdSource = "auto";
        private string _QualcommSource = "auto";
        private int _AppleSamplerIntervalMs = 1000;
        private int _WindowsGpuSamplerIntervalMs = 1000;
        private int _RequestTimeoutMs = 5000;
        private int _WarmupDelayMs = 1000;
        private int _SectionStaleAfterMs = 15000;
        private int _UtilyzeSampleStaleAfterMs = 5000;
    }
}
