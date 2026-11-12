namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;

    /// <summary>
    /// Shared telemetry component test double.
    /// </summary>
    public class TestTelemetryComponents :
        ISystemTelemetryProvider,
        IMemoryInfoProvider,
        INetworkRateSampler,
        INvidiaGpuTelemetryProvider,
        IAmdGpuTelemetryProvider,
        IAppleGpuTelemetryProvider,
        IQualcommGpuTelemetryProvider,
        IOllamaClient,
        IVllmTelemetryProvider,
        IUtilyzeTelemetryProvider,
        IRuntimeCapabilitiesService
    {
        /// <summary>
        /// Current runtime capabilities.
        /// </summary>
        public RuntimeCapabilities Current { get; set; } = new RuntimeCapabilities();

        /// <summary>
        /// Optional warmup handler.
        /// </summary>
        public Func<CancellationToken, Task>? WarmupHandler { get; set; } = null;

        /// <summary>
        /// Optional initialize handler.
        /// </summary>
        public Func<CancellationToken, Task>? InitializeHandler { get; set; } = null;

        /// <summary>
        /// Optional set warm handler.
        /// </summary>
        public Action<bool>? SetTelemetryWarmHandler { get; set; } = null;

        /// <summary>
        /// Optional system telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<SystemTelemetry>>? SystemTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional CPU telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<CpuTelemetry>>? CpuTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional disk telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<DiskTelemetry>>? DiskTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional memory telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<MemoryTelemetry>>? MemoryTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional network telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<NetworkTelemetry>>? NetworkTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional NVIDIA GPU telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<NvidiaGpuTelemetry?>>? NvidiaTelemetryHandler { get; set; } = null;

        /// <summary>
        /// AMD GPU telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<AmdGpuTelemetry?>>? AmdTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Apple GPU telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<AppleGpuTelemetry?>>? AppleTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Qualcomm GPU telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<QualcommGpuTelemetry?>>? QualcommTelemetryHandler { get; set; } = null;

        /// <summary>
        /// GPU probe handler (all vendors); defaults to the Current availability flags.
        /// </summary>
        public Func<GpuVendorEnum, CancellationToken, Task<GpuProbeResult>>? GpuProbeHandler { get; set; } = null;

        /// <summary>
        /// Optional Ollama telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<OllamaTelemetry?>>? OllamaTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional vLLM telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<VllmTelemetry?>>? VllmTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional Utilyze telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<UtilyzeTelemetry?>>? UtilyzeTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional Ollama availability handler.
        /// </summary>
        public Func<CancellationToken, Task<bool>>? OllamaAvailabilityHandler { get; set; } = null;

        /// <summary>
        /// Optional vLLM availability handler.
        /// </summary>
        public Func<CancellationToken, Task<bool>>? VllmAvailabilityHandler { get; set; } = null;

        /// <summary>
        /// Optional Utilyze availability handler.
        /// </summary>
        public Func<CancellationToken, Task<bool>>? UtilyzeAvailabilityHandler { get; set; } = null;

        /// <summary>
        /// Warm any sampler state required by the implementation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task WarmupAsync(CancellationToken cancellationToken)
        {
            if (WarmupHandler != null)
            {
                return WarmupHandler(cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieve system metadata.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>System metadata.</returns>
        public Task<SystemTelemetry> GetSystemTelemetryAsync(CancellationToken cancellationToken)
        {
            if (SystemTelemetryHandler != null)
            {
                return SystemTelemetryHandler(cancellationToken);
            }

            return Task.FromResult(new SystemTelemetry());
        }

        /// <summary>
        /// Retrieve CPU telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>CPU telemetry.</returns>
        public Task<CpuTelemetry> GetCpuTelemetryAsync(CancellationToken cancellationToken)
        {
            if (CpuTelemetryHandler != null)
            {
                return CpuTelemetryHandler(cancellationToken);
            }

            return Task.FromResult(new CpuTelemetry());
        }

        /// <summary>
        /// Retrieve disk telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Disk telemetry.</returns>
        public Task<DiskTelemetry> GetDiskTelemetryAsync(CancellationToken cancellationToken)
        {
            if (DiskTelemetryHandler != null)
            {
                return DiskTelemetryHandler(cancellationToken);
            }

            return Task.FromResult(new DiskTelemetry());
        }

        /// <summary>
        /// Capture memory telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Memory telemetry.</returns>
        public Task<MemoryTelemetry> GetMemoryTelemetryAsync(CancellationToken cancellationToken)
        {
            if (MemoryTelemetryHandler != null)
            {
                return MemoryTelemetryHandler(cancellationToken);
            }

            return Task.FromResult(new MemoryTelemetry());
        }

        /// <summary>
        /// Capture a current network sample.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Network telemetry.</returns>
        public Task<NetworkTelemetry> CaptureAsync(CancellationToken cancellationToken)
        {
            if (NetworkTelemetryHandler != null)
            {
                return NetworkTelemetryHandler(cancellationToken);
            }

            return Task.FromResult(new NetworkTelemetry());
        }

        GpuVendorEnum IGpuVendorTelemetryProvider<NvidiaGpuTelemetry>.Vendor => GpuVendorEnum.Nvidia;

        GpuVendorEnum IGpuVendorTelemetryProvider<AmdGpuTelemetry>.Vendor => GpuVendorEnum.Amd;

        GpuVendorEnum IGpuVendorTelemetryProvider<AppleGpuTelemetry>.Vendor => GpuVendorEnum.Apple;

        GpuVendorEnum IGpuVendorTelemetryProvider<QualcommGpuTelemetry>.Vendor => GpuVendorEnum.Qualcomm;

        Task<GpuProbeResult> IGpuVendorTelemetryProvider<NvidiaGpuTelemetry>.ProbeAsync(CancellationToken cancellationToken) => Probe(GpuVendorEnum.Nvidia, Current.NvidiaAvailable, cancellationToken);

        Task<GpuProbeResult> IGpuVendorTelemetryProvider<AmdGpuTelemetry>.ProbeAsync(CancellationToken cancellationToken) => Probe(GpuVendorEnum.Amd, Current.AmdAvailable, cancellationToken);

        Task<GpuProbeResult> IGpuVendorTelemetryProvider<AppleGpuTelemetry>.ProbeAsync(CancellationToken cancellationToken) => Probe(GpuVendorEnum.Apple, Current.AppleGpuAvailable, cancellationToken);

        Task<GpuProbeResult> IGpuVendorTelemetryProvider<QualcommGpuTelemetry>.ProbeAsync(CancellationToken cancellationToken) => Probe(GpuVendorEnum.Qualcomm, Current.QualcommAvailable, cancellationToken);

        Task<NvidiaGpuTelemetry?> IGpuVendorTelemetryProvider<NvidiaGpuTelemetry>.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            return NvidiaTelemetryHandler != null ? NvidiaTelemetryHandler(cancellationToken) : Task.FromResult<NvidiaGpuTelemetry?>(null);
        }

        Task<AmdGpuTelemetry?> IGpuVendorTelemetryProvider<AmdGpuTelemetry>.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            return AmdTelemetryHandler != null ? AmdTelemetryHandler(cancellationToken) : Task.FromResult<AmdGpuTelemetry?>(null);
        }

        Task<AppleGpuTelemetry?> IGpuVendorTelemetryProvider<AppleGpuTelemetry>.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            return AppleTelemetryHandler != null ? AppleTelemetryHandler(cancellationToken) : Task.FromResult<AppleGpuTelemetry?>(null);
        }

        Task<QualcommGpuTelemetry?> IGpuVendorTelemetryProvider<QualcommGpuTelemetry>.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            return QualcommTelemetryHandler != null ? QualcommTelemetryHandler(cancellationToken) : Task.FromResult<QualcommGpuTelemetry?>(null);
        }

        private Task<GpuProbeResult> Probe(GpuVendorEnum vendor, bool available, CancellationToken cancellationToken)
        {
            if (GpuProbeHandler != null)
            {
                return GpuProbeHandler(vendor, cancellationToken);
            }

            return Task.FromResult(new GpuProbeResult { Vendor = vendor, Available = available });
        }

        /// <summary>
        /// Determine whether Ollama is available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when reachable.</returns>
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            if (OllamaAvailabilityHandler != null)
            {
                return OllamaAvailabilityHandler(cancellationToken);
            }

            return Task.FromResult(Current.OllamaAvailable);
        }

        /// <summary>
        /// Capture Ollama telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ollama telemetry when reachable.</returns>
        Task<OllamaTelemetry?> IOllamaClient.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            if (OllamaTelemetryHandler != null)
            {
                return OllamaTelemetryHandler(cancellationToken);
            }

            return Task.FromResult<OllamaTelemetry?>(null);
        }

        /// <summary>
        /// Determine whether vLLM is available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when reachable.</returns>
        Task<bool> IVllmTelemetryProvider.IsAvailableAsync(CancellationToken cancellationToken)
        {
            if (VllmAvailabilityHandler != null)
            {
                return VllmAvailabilityHandler(cancellationToken);
            }

            return Task.FromResult(Current.VllmAvailable);
        }

        /// <summary>
        /// Capture vLLM telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>vLLM telemetry when reachable.</returns>
        Task<VllmTelemetry?> IVllmTelemetryProvider.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            if (VllmTelemetryHandler != null)
            {
                return VllmTelemetryHandler(cancellationToken);
            }

            return Task.FromResult<VllmTelemetry?>(null);
        }

        /// <summary>
        /// Determine whether Utilyze is available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when reachable.</returns>
        Task<bool> IUtilyzeTelemetryProvider.IsAvailableAsync(CancellationToken cancellationToken)
        {
            if (UtilyzeAvailabilityHandler != null)
            {
                return UtilyzeAvailabilityHandler(cancellationToken);
            }

            return Task.FromResult(Current.UtilyzeAvailable);
        }

        /// <summary>
        /// Capture Utilyze telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Utilyze telemetry when reachable.</returns>
        Task<UtilyzeTelemetry?> IUtilyzeTelemetryProvider.GetTelemetryAsync(CancellationToken cancellationToken)
        {
            if (UtilyzeTelemetryHandler != null)
            {
                return UtilyzeTelemetryHandler(cancellationToken);
            }

            return Task.FromResult<UtilyzeTelemetry?>(null);
        }

        /// <summary>
        /// Initialize capability detection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (InitializeHandler != null)
            {
                return InitializeHandler(cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Update telemetry warm status.
        /// </summary>
        /// <param name="isWarm">Warm status.</param>
        public void SetTelemetryWarm(bool isWarm)
        {
            Current.TelemetryWarm = isWarm;
            if (SetTelemetryWarmHandler != null)
            {
                SetTelemetryWarmHandler(isWarm);
            }
        }
    }
}
