namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// System telemetry provider contract.
    /// </summary>
    public interface ISystemTelemetryProvider
    {
        /// <summary>
        /// Warm any sampler state required by the implementation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WarmupAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve system metadata.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>System metadata.</returns>
        Task<SystemTelemetry> GetSystemTelemetryAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve CPU telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>CPU telemetry.</returns>
        Task<CpuTelemetry> GetCpuTelemetryAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve disk telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Disk telemetry.</returns>
        Task<DiskTelemetry> GetDiskTelemetryAsync(CancellationToken cancellationToken);
    }
}
