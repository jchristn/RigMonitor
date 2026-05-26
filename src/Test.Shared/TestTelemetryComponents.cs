namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;

    /// <summary>
    /// Shared telemetry component test double.
    /// </summary>
    public class TestTelemetryComponents :
        ISystemTelemetryProvider,
        IMemoryInfoProvider,
        INetworkRateSampler,
        IGpuTelemetryProvider,
        IOllamaClient,
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
        /// Optional GPU telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<GpuTelemetry?>>? GpuTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional Ollama telemetry handler.
        /// </summary>
        public Func<CancellationToken, Task<OllamaTelemetry?>>? OllamaTelemetryHandler { get; set; } = null;

        /// <summary>
        /// Optional Ollama availability handler.
        /// </summary>
        public Func<CancellationToken, Task<bool>>? OllamaAvailabilityHandler { get; set; } = null;

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

        /// <summary>
        /// Capture GPU telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>GPU telemetry when available.</returns>
        public Task<GpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            if (GpuTelemetryHandler != null)
            {
                return GpuTelemetryHandler(cancellationToken);
            }

            return Task.FromResult<GpuTelemetry?>(null);
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
