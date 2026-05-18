namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
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
        private readonly RuntimeCapabilities _Current;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="dashboardEnabled">Whether the dashboard is enabled.</param>
        /// <param name="dcgmClient">DCGM client.</param>
        /// <param name="ollamaClient">Ollama client.</param>
        public RuntimeCapabilitiesService(
            TelemetrySettings settings,
            bool dashboardEnabled,
            IDcgmExporterClient dcgmClient,
            IOllamaClient ollamaClient)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (dcgmClient == null) throw new ArgumentNullException(nameof(dcgmClient));
            if (ollamaClient == null) throw new ArgumentNullException(nameof(ollamaClient));

            _Settings = settings;
            _DcgmClient = dcgmClient;
            _OllamaClient = ollamaClient;
            _Current = new RuntimeCapabilities
            {
                DashboardEnabled = dashboardEnabled,
                DcgmExporterUrl = settings.DcgmExporterUrl,
                OllamaBaseUrl = settings.OllamaBaseUrl,
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
            _Current.NvidiaAvailable = await ProbeDcgmAsync(cancellationToken).ConfigureAwait(false);
            _Current.OllamaAvailable = await _OllamaClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
            _Current.CollectedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Update telemetry warm status.
        /// </summary>
        /// <param name="isWarm">Warm status.</param>
        public void SetTelemetryWarm(bool isWarm)
        {
            _Current.TelemetryWarm = isWarm;
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
