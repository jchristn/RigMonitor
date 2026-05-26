namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
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
        private readonly TimeProvider _TimeProvider;
        private readonly Dictionary<string, TelemetrySectionCollectionStatus> _SectionStates;
        private readonly object _SectionStateLock = new object();
        private bool _IsWarm = false;

        private const string _SystemSection = "system";
        private const string _CpuSection = "cpu";
        private const string _MemorySection = "memory";
        private const string _NetworkSection = "network";
        private const string _DiskSection = "disk";
        private const string _GpuSection = "gpu";
        private const string _OllamaSection = "ollama";

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
        /// <param name="timeProvider">Time provider used for timestamps and durations.</param>
        public TelemetryService(
            TelemetrySettings settings,
            ISystemTelemetryProvider systemTelemetryProvider,
            IMemoryInfoProvider memoryInfoProvider,
            INetworkRateSampler networkRateSampler,
            IRuntimeCapabilitiesService runtimeCapabilitiesService,
            IGpuTelemetryProvider gpuTelemetryProvider,
            IOllamaClient ollamaClient,
            TimeProvider? timeProvider = null)
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
            _TimeProvider = timeProvider ?? TimeProvider.System;
            _SectionStates = CreateSectionStates();
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
            DateTime collectedUtc = _TimeProvider.GetUtcNow().UtcDateTime;

            TelemetrySnapshot snapshot = new TelemetrySnapshot
            {
                CollectedUtc = collectedUtc,
                HostPlatform = capabilities.HostPlatform,
                NvidiaAvailable = capabilities.NvidiaAvailable,
                OllamaAvailable = capabilities.OllamaAvailable
            };

            TelemetryCollectionMetadata collection = new TelemetryCollectionMetadata
            {
                CollectedUtc = collectedUtc,
                StaleAfterMs = _Settings.SectionStaleAfterMs
            };

            collection.System = await CollectSectionAsync(
                _SystemSection,
                requestOptions.IncludeSystem,
                true,
                async (token) => await _SystemTelemetryProvider.GetSystemTelemetryAsync(token).ConfigureAwait(false),
                value => snapshot.System = value,
                cancellationToken).ConfigureAwait(false);

            collection.Cpu = await CollectSectionAsync(
                _CpuSection,
                requestOptions.IncludeCpu,
                true,
                async (token) => await _SystemTelemetryProvider.GetCpuTelemetryAsync(token).ConfigureAwait(false),
                value => snapshot.Cpu = value,
                cancellationToken).ConfigureAwait(false);

            collection.Memory = await CollectSectionAsync(
                _MemorySection,
                requestOptions.IncludeMemory,
                true,
                async (token) => await _MemoryInfoProvider.GetMemoryTelemetryAsync(token).ConfigureAwait(false),
                value => snapshot.Memory = value,
                cancellationToken).ConfigureAwait(false);

            collection.Network = await CollectSectionAsync(
                _NetworkSection,
                requestOptions.IncludeNetwork,
                true,
                async (token) => await _NetworkRateSampler.CaptureAsync(token).ConfigureAwait(false),
                value => snapshot.Network = value,
                cancellationToken).ConfigureAwait(false);

            collection.Disk = await CollectSectionAsync(
                _DiskSection,
                requestOptions.IncludeDisk,
                true,
                async (token) => await _SystemTelemetryProvider.GetDiskTelemetryAsync(token).ConfigureAwait(false),
                value => snapshot.Disk = value,
                cancellationToken).ConfigureAwait(false);

            collection.Gpu = await CollectSectionAsync(
                _GpuSection,
                requestOptions.IncludeGpu,
                capabilities.NvidiaAvailable,
                async (token) => await _GpuTelemetryProvider.GetTelemetryAsync(token).ConfigureAwait(false),
                value => snapshot.Gpu = value,
                cancellationToken).ConfigureAwait(false);

            collection.Ollama = await CollectSectionAsync(
                _OllamaSection,
                requestOptions.IncludeOllama,
                capabilities.OllamaAvailable,
                async (token) => await _OllamaClient.GetTelemetryAsync(token).ConfigureAwait(false),
                value => snapshot.Ollama = value,
                cancellationToken).ConfigureAwait(false);

            snapshot.Collection = collection;

            return snapshot;
        }

        private async Task<TelemetrySectionCollectionStatus> CollectSectionAsync<TTelemetry>(
            string sectionName,
            bool requested,
            bool supported,
            Func<CancellationToken, Task<TTelemetry?>> collector,
            Action<TTelemetry> applyTelemetry,
            CancellationToken cancellationToken) where TTelemetry : class
        {
            if (!requested)
            {
                return BuildResponseStatus(sectionName, false, supported, _TimeProvider.GetUtcNow().UtcDateTime);
            }

            if (!supported)
            {
                return BuildResponseStatus(sectionName, true, false, _TimeProvider.GetUtcNow().UtcDateTime);
            }

            DateTime attemptUtc = _TimeProvider.GetUtcNow().UtcDateTime;
            long startTimestamp = _TimeProvider.GetTimestamp();

            try
            {
                TTelemetry? telemetry = await collector(cancellationToken).ConfigureAwait(false);
                double durationMs = _TimeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;

                if (telemetry != null)
                {
                    applyTelemetry(telemetry);
                    UpdateTrackedState(sectionName, TelemetryCollectionStatusCodeEnum.Ok, true, attemptUtc, durationMs, null, true);
                }
                else
                {
                    UpdateTrackedState(
                        sectionName,
                        TelemetryCollectionStatusCodeEnum.Unavailable,
                        false,
                        attemptUtc,
                        durationMs,
                        null,
                        true);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                double durationMs = _TimeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
                UpdateTrackedState(
                    sectionName,
                    TelemetryCollectionStatusCodeEnum.Error,
                    false,
                    attemptUtc,
                    durationMs,
                    "Collection timed out before the section returned a sample.",
                    true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (PlatformNotSupportedException exception)
            {
                double durationMs = _TimeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
                UpdateTrackedState(
                    sectionName,
                    TelemetryCollectionStatusCodeEnum.Unsupported,
                    false,
                    attemptUtc,
                    durationMs,
                    exception.Message,
                    false);
                supported = false;
            }
            catch (NotSupportedException exception)
            {
                double durationMs = _TimeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
                UpdateTrackedState(
                    sectionName,
                    TelemetryCollectionStatusCodeEnum.Unsupported,
                    false,
                    attemptUtc,
                    durationMs,
                    exception.Message,
                    false);
                supported = false;
            }
            catch (Exception exception)
            {
                double durationMs = _TimeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
                UpdateTrackedState(
                    sectionName,
                    TelemetryCollectionStatusCodeEnum.Error,
                    false,
                    attemptUtc,
                    durationMs,
                    exception.Message,
                    true);
            }

            return BuildResponseStatus(sectionName, true, supported, _TimeProvider.GetUtcNow().UtcDateTime);
        }

        private TelemetrySectionCollectionStatus BuildResponseStatus(string sectionName, bool requested, bool supported, DateTime referenceUtc)
        {
            TelemetrySectionCollectionStatus tracked = GetTrackedState(sectionName);
            TelemetrySectionFreshness freshness = BuildFreshness(requested, supported, tracked.LastSuccessUtc, referenceUtc);
            TelemetryCollectionStatusCodeEnum statusCode = DetermineStatusCode(requested, supported, tracked.StatusCode, freshness.Status);

            return new TelemetrySectionCollectionStatus
            {
                Requested = requested,
                Supported = supported,
                StatusCode = statusCode,
                LastAttemptUtc = tracked.LastAttemptUtc,
                LastSuccessUtc = tracked.LastSuccessUtc,
                LastDurationMs = tracked.LastDurationMs,
                Freshness = freshness,
                Message = BuildStatusMessage(sectionName, statusCode, freshness.Status),
                LastError = tracked.LastError
            };
        }

        private TelemetrySectionFreshness BuildFreshness(bool requested, bool supported, DateTime? lastSuccessUtc, DateTime referenceUtc)
        {
            TelemetrySectionFreshness freshness = new TelemetrySectionFreshness
            {
                StaleAfterMs = _Settings.SectionStaleAfterMs
            };

            if (!requested || !supported)
            {
                freshness.Status = TelemetryFreshnessStatusEnum.NotApplicable;
                return freshness;
            }

            if (!lastSuccessUtc.HasValue)
            {
                freshness.Status = TelemetryFreshnessStatusEnum.Unknown;
                return freshness;
            }

            double ageMs = Math.Max(0D, (referenceUtc - lastSuccessUtc.Value).TotalMilliseconds);
            freshness.AgeMs = ageMs;
            freshness.Status = ageMs > _Settings.SectionStaleAfterMs
                ? TelemetryFreshnessStatusEnum.Stale
                : TelemetryFreshnessStatusEnum.Fresh;
            return freshness;
        }

        private TelemetryCollectionStatusCodeEnum DetermineStatusCode(
            bool requested,
            bool supported,
            TelemetryCollectionStatusCodeEnum trackedStatusCode,
            TelemetryFreshnessStatusEnum freshnessStatus)
        {
            if (!requested)
            {
                return TelemetryCollectionStatusCodeEnum.Disabled;
            }

            if (!supported)
            {
                return TelemetryCollectionStatusCodeEnum.Unsupported;
            }

            if (freshnessStatus == TelemetryFreshnessStatusEnum.Stale
                && trackedStatusCode != TelemetryCollectionStatusCodeEnum.Ok)
            {
                return TelemetryCollectionStatusCodeEnum.Stale;
            }

            return trackedStatusCode;
        }

        private string BuildStatusMessage(
            string sectionName,
            TelemetryCollectionStatusCodeEnum statusCode,
            TelemetryFreshnessStatusEnum freshnessStatus)
        {
            string displayName = GetSectionDisplayName(sectionName);

            if (statusCode == TelemetryCollectionStatusCodeEnum.Disabled)
            {
                return displayName + " telemetry was intentionally not requested.";
            }

            if (statusCode == TelemetryCollectionStatusCodeEnum.Unsupported)
            {
                return displayName + " telemetry is unsupported on this host.";
            }

            if (statusCode == TelemetryCollectionStatusCodeEnum.Ok)
            {
                return displayName + " telemetry collected successfully.";
            }

            if (statusCode == TelemetryCollectionStatusCodeEnum.Stale)
            {
                return displayName + " telemetry is stale because the most recent successful sample is older than the freshness window.";
            }

            if (statusCode == TelemetryCollectionStatusCodeEnum.Error)
            {
                return displayName + " telemetry collection failed.";
            }

            if (freshnessStatus == TelemetryFreshnessStatusEnum.Unknown)
            {
                return displayName + " telemetry is temporarily unavailable and no successful sample has been recorded yet.";
            }

            return displayName + " telemetry is temporarily unavailable.";
        }

        private TelemetrySectionCollectionStatus GetTrackedState(string sectionName)
        {
            lock (_SectionStateLock)
            {
                if (_SectionStates.TryGetValue(sectionName, out TelemetrySectionCollectionStatus? state))
                {
                    return CloneStatus(state);
                }
            }

            return new TelemetrySectionCollectionStatus();
        }

        private void UpdateTrackedState(
            string sectionName,
            TelemetryCollectionStatusCodeEnum statusCode,
            bool success,
            DateTime attemptUtc,
            double durationMs,
            string? lastError,
            bool supported)
        {
            lock (_SectionStateLock)
            {
                if (!_SectionStates.TryGetValue(sectionName, out TelemetrySectionCollectionStatus? state))
                {
                    state = new TelemetrySectionCollectionStatus();
                    _SectionStates[sectionName] = state;
                }

                state.Supported = supported;
                state.StatusCode = statusCode;
                state.LastAttemptUtc = attemptUtc;
                state.LastDurationMs = durationMs;
                state.LastError = lastError;

                if (success)
                {
                    state.LastSuccessUtc = attemptUtc;
                    state.LastError = null;
                }
            }
        }

        private static TelemetrySectionCollectionStatus CloneStatus(TelemetrySectionCollectionStatus input)
        {
            return new TelemetrySectionCollectionStatus
            {
                Requested = input.Requested,
                Supported = input.Supported,
                StatusCode = input.StatusCode,
                LastAttemptUtc = input.LastAttemptUtc,
                LastSuccessUtc = input.LastSuccessUtc,
                LastDurationMs = input.LastDurationMs,
                Freshness = input.Freshness == null
                    ? null
                    : new TelemetrySectionFreshness
                    {
                        Status = input.Freshness.Status,
                        AgeMs = input.Freshness.AgeMs,
                        StaleAfterMs = input.Freshness.StaleAfterMs
                    },
                Message = input.Message,
                LastError = input.LastError
            };
        }

        private static Dictionary<string, TelemetrySectionCollectionStatus> CreateSectionStates()
        {
            return new Dictionary<string, TelemetrySectionCollectionStatus>(StringComparer.OrdinalIgnoreCase)
            {
                { _SystemSection, new TelemetrySectionCollectionStatus() },
                { _CpuSection, new TelemetrySectionCollectionStatus() },
                { _MemorySection, new TelemetrySectionCollectionStatus() },
                { _NetworkSection, new TelemetrySectionCollectionStatus() },
                { _DiskSection, new TelemetrySectionCollectionStatus() },
                { _GpuSection, new TelemetrySectionCollectionStatus() },
                { _OllamaSection, new TelemetrySectionCollectionStatus() }
            };
        }

        private static string GetSectionDisplayName(string sectionName)
        {
            if (String.Equals(sectionName, _CpuSection, StringComparison.OrdinalIgnoreCase))
            {
                return "CPU";
            }

            if (String.Equals(sectionName, _GpuSection, StringComparison.OrdinalIgnoreCase))
            {
                return "GPU";
            }

            if (String.Equals(sectionName, _OllamaSection, StringComparison.OrdinalIgnoreCase))
            {
                return "Ollama";
            }

            if (String.IsNullOrWhiteSpace(sectionName))
            {
                return "Telemetry";
            }

            return Char.ToUpperInvariant(sectionName[0]) + sectionName.Substring(1).ToLowerInvariant();
        }
    }
}
