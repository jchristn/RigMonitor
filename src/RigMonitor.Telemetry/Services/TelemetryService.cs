namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Telemetry aggregation service.
    /// </summary>
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetrySettings _Settings;
        private readonly ISystemTelemetryProvider _SystemTelemetryProvider;
        private readonly IMemoryInfoProvider _MemoryInfoProvider;
        private readonly INetworkRateSampler _NetworkRateSampler;
        private readonly IRuntimeCapabilitiesService _RuntimeCapabilitiesService;
        private readonly IGpuTelemetryProvider _GpuTelemetryProvider;
        private readonly IOllamaClient _OllamaClient;
        private bool _IsWarm = false;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="systemTelemetryProvider">System provider.</param>
        /// <param name="memoryInfoProvider">Memory provider.</param>
        /// <param name="networkRateSampler">Network sampler.</param>
        /// <param name="runtimeCapabilitiesService">Capabilities service.</param>
        /// <param name="gpuTelemetryProvider">GPU provider.</param>
        /// <param name="ollamaClient">Ollama client.</param>
        public TelemetryService(
            TelemetrySettings settings,
            ISystemTelemetryProvider systemTelemetryProvider,
            IMemoryInfoProvider memoryInfoProvider,
            INetworkRateSampler networkRateSampler,
            IRuntimeCapabilitiesService runtimeCapabilitiesService,
            IGpuTelemetryProvider gpuTelemetryProvider,
            IOllamaClient ollamaClient)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (systemTelemetryProvider == null) throw new ArgumentNullException(nameof(systemTelemetryProvider));
            if (memoryInfoProvider == null) throw new ArgumentNullException(nameof(memoryInfoProvider));
            if (networkRateSampler == null) throw new ArgumentNullException(nameof(networkRateSampler));
            if (runtimeCapabilitiesService == null) throw new ArgumentNullException(nameof(runtimeCapabilitiesService));
            if (gpuTelemetryProvider == null) throw new ArgumentNullException(nameof(gpuTelemetryProvider));
            if (ollamaClient == null) throw new ArgumentNullException(nameof(ollamaClient));

            _Settings = settings;
            _SystemTelemetryProvider = systemTelemetryProvider;
            _MemoryInfoProvider = memoryInfoProvider;
            _NetworkRateSampler = networkRateSampler;
            _RuntimeCapabilitiesService = runtimeCapabilitiesService;
            _GpuTelemetryProvider = gpuTelemetryProvider;
            _OllamaClient = ollamaClient;
        }

        /// <summary>
        /// Whether warmup completed.
        /// </summary>
        public bool IsWarm
        {
            get
            {
                return _IsWarm;
            }
        }

        /// <summary>
        /// Warm the telemetry subsystem.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task WarmupAsync(CancellationToken cancellationToken)
        {
            await _SystemTelemetryProvider.WarmupAsync(cancellationToken).ConfigureAwait(false);
            await _NetworkRateSampler.WarmupAsync(cancellationToken).ConfigureAwait(false);

            if (_Settings.WarmupDelayMs > 0)
            {
                await Task.Delay(_Settings.WarmupDelayMs, cancellationToken).ConfigureAwait(false);
            }

            _IsWarm = true;
            _RuntimeCapabilitiesService.SetTelemetryWarm(true);
        }

        /// <summary>
        /// Capture a full telemetry snapshot.
        /// </summary>
        /// <param name="requestOptions">Section selection options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Telemetry snapshot.</returns>
        public async Task<TelemetrySnapshot> GetSnapshotAsync(TelemetryRequestOptions requestOptions, CancellationToken cancellationToken)
        {
            if (requestOptions == null) throw new ArgumentNullException(nameof(requestOptions));

            RuntimeCapabilities capabilities = _RuntimeCapabilitiesService.Current;

            TelemetrySnapshot snapshot = new TelemetrySnapshot
            {
                CollectedUtc = DateTime.UtcNow,
                HostPlatform = capabilities.HostPlatform,
                NvidiaAvailable = capabilities.NvidiaAvailable,
                OllamaAvailable = capabilities.OllamaAvailable
            };

            if (requestOptions.IncludeSystem)
            {
                snapshot.System = await _SystemTelemetryProvider.GetSystemTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (requestOptions.IncludeCpu)
            {
                snapshot.Cpu = await _SystemTelemetryProvider.GetCpuTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (requestOptions.IncludeMemory)
            {
                snapshot.Memory = await _MemoryInfoProvider.GetMemoryTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (requestOptions.IncludeNetwork)
            {
                snapshot.Network = await _NetworkRateSampler.CaptureAsync(cancellationToken).ConfigureAwait(false);
            }

            if (requestOptions.IncludeDisk)
            {
                snapshot.Disk = await _SystemTelemetryProvider.GetDiskTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (requestOptions.IncludeGpu && capabilities.NvidiaAvailable)
            {
                snapshot.Gpu = await _GpuTelemetryProvider.GetTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (requestOptions.IncludeOllama && capabilities.OllamaAvailable)
            {
                snapshot.Ollama = await _OllamaClient.GetTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            return snapshot;
        }
    }
}
