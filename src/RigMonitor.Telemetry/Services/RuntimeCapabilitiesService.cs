namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
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
        private readonly INvidiaGpuTelemetryProvider _NvidiaProvider;
        private readonly IAmdGpuTelemetryProvider _AmdProvider;
        private readonly IAppleGpuTelemetryProvider _AppleProvider;
        private readonly IQualcommGpuTelemetryProvider _QualcommProvider;
        private readonly IOllamaClient _OllamaClient;
        private readonly IVllmTelemetryProvider _VllmTelemetryProvider;
        private readonly IUtilyzeTelemetryProvider _UtilyzeTelemetryProvider;
        private readonly RuntimeCapabilities _Current;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="dashboardEnabled">Whether the dashboard is enabled.</param>
        /// <param name="nvidiaProvider">NVIDIA provider.</param>
        /// <param name="amdProvider">AMD provider.</param>
        /// <param name="appleProvider">Apple provider.</param>
        /// <param name="qualcommProvider">Qualcomm provider.</param>
        /// <param name="ollamaClient">Ollama client.</param>
        /// <param name="vllmTelemetryProvider">vLLM provider.</param>
        /// <param name="utilyzeTelemetryProvider">Utilyze provider.</param>
        /// <param name="hideUnsupportedGpuSections">Dashboard default for hiding unsupported GPU vendor sections.</param>
        public RuntimeCapabilitiesService(
            TelemetrySettings settings,
            bool dashboardEnabled,
            INvidiaGpuTelemetryProvider nvidiaProvider,
            IAmdGpuTelemetryProvider amdProvider,
            IAppleGpuTelemetryProvider appleProvider,
            IQualcommGpuTelemetryProvider qualcommProvider,
            IOllamaClient ollamaClient,
            IVllmTelemetryProvider vllmTelemetryProvider,
            IUtilyzeTelemetryProvider utilyzeTelemetryProvider,
            bool hideUnsupportedGpuSections = false)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (nvidiaProvider == null) throw new ArgumentNullException(nameof(nvidiaProvider));
            if (amdProvider == null) throw new ArgumentNullException(nameof(amdProvider));
            if (appleProvider == null) throw new ArgumentNullException(nameof(appleProvider));
            if (qualcommProvider == null) throw new ArgumentNullException(nameof(qualcommProvider));
            if (ollamaClient == null) throw new ArgumentNullException(nameof(ollamaClient));
            if (vllmTelemetryProvider == null) throw new ArgumentNullException(nameof(vllmTelemetryProvider));
            if (utilyzeTelemetryProvider == null) throw new ArgumentNullException(nameof(utilyzeTelemetryProvider));

            _Settings = settings;
            _NvidiaProvider = nvidiaProvider;
            _AmdProvider = amdProvider;
            _AppleProvider = appleProvider;
            _QualcommProvider = qualcommProvider;
            _OllamaClient = ollamaClient;
            _VllmTelemetryProvider = vllmTelemetryProvider;
            _UtilyzeTelemetryProvider = utilyzeTelemetryProvider;
            _Current = new RuntimeCapabilities
            {
                DashboardEnabled = dashboardEnabled,
                HideUnsupportedGpuSections = hideUnsupportedGpuSections,
                DcgmExporterUrl = settings.DcgmExporterUrl,
                AmdMetricsExporterUrl = settings.AmdMetricsExporterUrl,
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
                    await ProbeGpusAsync(cancellationToken).ConfigureAwait(false);
                    _Current.OllamaAvailable = await _OllamaClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
                    _Current.VllmAvailable = await _VllmTelemetryProvider.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
                    _Current.UtilyzeAvailable = await _UtilyzeTelemetryProvider.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
                    _Current.CollectedUtc = DateTime.UtcNow;

                    RigMonitorInstrumentation.SetCapability("nvidia", _Current.NvidiaAvailable);
                    RigMonitorInstrumentation.SetCapability("amd", _Current.AmdAvailable);
                    RigMonitorInstrumentation.SetCapability("apple", _Current.AppleGpuAvailable);
                    RigMonitorInstrumentation.SetCapability("qualcomm", _Current.QualcommAvailable);
                    RigMonitorInstrumentation.SetCapability("ollama", _Current.OllamaAvailable);
                    RigMonitorInstrumentation.SetCapability("vllm", _Current.VllmAvailable);
                    RigMonitorInstrumentation.SetCapability("utilyze", _Current.UtilyzeAvailable);
                    activity?.SetTag("rigmonitor.capability.nvidia", _Current.NvidiaAvailable);
                    activity?.SetTag("rigmonitor.capability.amd", _Current.AmdAvailable);
                    activity?.SetTag("rigmonitor.capability.apple", _Current.AppleGpuAvailable);
                    activity?.SetTag("rigmonitor.capability.qualcomm", _Current.QualcommAvailable);
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

        private async Task ProbeGpusAsync(CancellationToken cancellationToken)
        {
            Task<GpuProbeResult> nvidia = SafeProbeAsync(GpuVendorEnum.Nvidia, token => _NvidiaProvider.ProbeAsync(token), cancellationToken);
            Task<GpuProbeResult> amd = SafeProbeAsync(GpuVendorEnum.Amd, token => _AmdProvider.ProbeAsync(token), cancellationToken);
            Task<GpuProbeResult> apple = SafeProbeAsync(GpuVendorEnum.Apple, token => _AppleProvider.ProbeAsync(token), cancellationToken);
            Task<GpuProbeResult> qualcomm = SafeProbeAsync(GpuVendorEnum.Qualcomm, token => _QualcommProvider.ProbeAsync(token), cancellationToken);
            await Task.WhenAll(nvidia, amd, apple, qualcomm).ConfigureAwait(false);

            GpuProbeResult nvidiaResult = await nvidia.ConfigureAwait(false);
            GpuProbeResult amdResult = await amd.ConfigureAwait(false);
            GpuProbeResult appleResult = await apple.ConfigureAwait(false);
            GpuProbeResult qualcommResult = await qualcomm.ConfigureAwait(false);

            _Current.NvidiaAvailable = nvidiaResult.Available;
            _Current.AmdAvailable = amdResult.Available;
            _Current.AppleGpuAvailable = appleResult.Available;
            _Current.QualcommAvailable = qualcommResult.Available;
            _Current.AmdSource = amdResult.Available ? amdResult.Source : null;
            _Current.QualcommSource = qualcommResult.Available ? qualcommResult.Source : null;
            _Current.AppleFeatures = appleResult.Available ? new List<string>(appleResult.Features) : new List<string>();
            _Current.GpuProviders = new List<GpuProbeResult> { nvidiaResult, amdResult, appleResult, qualcommResult };
        }

        private static async Task<GpuProbeResult> SafeProbeAsync(
            GpuVendorEnum vendor,
            Func<CancellationToken, Task<GpuProbeResult>> probe,
            CancellationToken cancellationToken)
        {
            try
            {
                GpuProbeResult result = await probe(cancellationToken).ConfigureAwait(false);
                result.Vendor = vendor;
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                RigMonitorInstrumentation.RecordError("capabilities." + vendor.ToString().ToLowerInvariant(), exception);
                return GpuProbeResult.Unavailable(vendor, exception.Message);
            }
        }
    }
}
