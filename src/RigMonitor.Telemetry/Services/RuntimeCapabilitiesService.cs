namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Platform.Shared;

    /// <summary>
    /// Runtime capability detector.
    /// </summary>
    public class RuntimeCapabilitiesService : IRuntimeCapabilitiesService
    {
        private readonly TelemetrySettings _Settings;
        private readonly IDcgmExporterClient _DcgmClient;
        private readonly IOllamaClient _OllamaClient;
        private readonly IVllmTelemetryProvider _VllmTelemetryProvider;
        private readonly IUtilyzeTelemetryProvider _UtilyzeTelemetryProvider;
        private readonly RuntimeCapabilities _Current;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="dashboardEnabled">Whether the dashboard is enabled.</param>
        /// <param name="dcgmClient">DCGM client.</param>
        /// <param name="ollamaClient">Ollama client.</param>
        /// <param name="vllmTelemetryProvider">vLLM provider.</param>
        /// <param name="utilyzeTelemetryProvider">Utilyze provider.</param>
        public RuntimeCapabilitiesService(
            TelemetrySettings settings,
            bool dashboardEnabled,
            IDcgmExporterClient dcgmClient,
            IOllamaClient ollamaClient,
            IVllmTelemetryProvider vllmTelemetryProvider,
            IUtilyzeTelemetryProvider utilyzeTelemetryProvider)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (dcgmClient == null) throw new ArgumentNullException(nameof(dcgmClient));
            if (ollamaClient == null) throw new ArgumentNullException(nameof(ollamaClient));
            if (vllmTelemetryProvider == null) throw new ArgumentNullException(nameof(vllmTelemetryProvider));
            if (utilyzeTelemetryProvider == null) throw new ArgumentNullException(nameof(utilyzeTelemetryProvider));

            _Settings = settings;
            _DcgmClient = dcgmClient;
            _OllamaClient = ollamaClient;
            _VllmTelemetryProvider = vllmTelemetryProvider;
            _UtilyzeTelemetryProvider = utilyzeTelemetryProvider;
            _Current = new RuntimeCapabilities
            {
                DashboardEnabled = dashboardEnabled,
                DcgmExporterUrl = settings.DcgmExporterUrl,
                OllamaBaseUrl = settings.OllamaBaseUrl,
                VllmEnabled = settings.VllmEnabled,
                VllmMetricsUrl = settings.VllmMetricsUrl,
                UtilyzeEnabled = settings.UtilyzeEnabled,
                UtilyzeLiveUrl = settings.UtilyzeLiveUrl,
                HostPlatform = PlatformHelpers.GetHostPlatform(),
                CollectedUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Current runtime capability snapshot.
        /// </summary>
        public RuntimeCapabilities Current
        {
            get
            {
                return _Current;
            }
        }

        /// <summary>
        /// Initialize capability detection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            using (Activity? activity = RigMonitorInstrumentation.StartActivity(RigMonitorTelemetryNames.SpanCapabilitiesProbe))
            {
                try
                {
                    _Current.NvidiaAvailable = await ProbeDcgmAsync(cancellationToken).ConfigureAwait(false);
                    _Current.OllamaAvailable = await _OllamaClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
                    _Current.VllmAvailable = await _VllmTelemetryProvider.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
                    _Current.UtilyzeAvailable = await _UtilyzeTelemetryProvider.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
                    _Current.CollectedUtc = DateTime.UtcNow;

                    RigMonitorInstrumentation.SetCapability("nvidia", _Current.NvidiaAvailable);
                    RigMonitorInstrumentation.SetCapability("ollama", _Current.OllamaAvailable);
                    RigMonitorInstrumentation.SetCapability("vllm", _Current.VllmAvailable);
                    RigMonitorInstrumentation.SetCapability("utilyze", _Current.UtilyzeAvailable);
                    activity?.SetTag("rigmonitor.capability.nvidia", _Current.NvidiaAvailable);
                    activity?.SetTag("rigmonitor.capability.ollama", _Current.OllamaAvailable);
                    activity?.SetTag("rigmonitor.capability.vllm", _Current.VllmAvailable);
                    activity?.SetTag("rigmonitor.capability.utilyze", _Current.UtilyzeAvailable);
                    RigMonitorInstrumentation.SetOk(activity);
                }
                catch (Exception exception)
                {
                    if (!(exception is OperationCanceledException))
                    {
                        RigMonitorInstrumentation.SetError(activity, exception);
                        RigMonitorInstrumentation.RecordError("capabilities", exception);
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Update telemetry warm status.
        /// </summary>
        /// <param name="isWarm">Warm status.</param>
        public void SetTelemetryWarm(bool isWarm)
        {
            _Current.TelemetryWarm = isWarm;
            RigMonitorInstrumentation.SetTelemetryWarm(isWarm);
            _Current.CollectedUtc = DateTime.UtcNow;
        }

        private async Task<bool> ProbeDcgmAsync(CancellationToken cancellationToken)
        {
            string? metrics = await _DcgmClient.TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(metrics))
            {
                return false;
            }

            return metrics.Contains("DCGM_FI_DEV_GPU_UTIL", StringComparison.OrdinalIgnoreCase)
                || metrics.Contains("DCGM_FI_DEV_FB_USED", StringComparison.OrdinalIgnoreCase)
                || metrics.Contains("DCGM_FI_DEV_GPU_TEMP", StringComparison.OrdinalIgnoreCase);
        }
    }
}
