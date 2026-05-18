namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Runtime capability service contract.
    /// </summary>
    public interface IRuntimeCapabilitiesService
    {
        /// <summary>
        /// Current runtime capability snapshot.
        /// </summary>
        RuntimeCapabilities Current { get; }

        /// <summary>
        /// Initialize capability detection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitializeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Update telemetry warm status.
        /// </summary>
        /// <param name="isWarm">Warm status.</param>
        void SetTelemetryWarm(bool isWarm);
    }
}
