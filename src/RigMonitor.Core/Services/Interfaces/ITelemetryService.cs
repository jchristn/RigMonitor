namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Telemetry aggregation service contract.
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>
        /// Whether warmup completed.
        /// </summary>
        bool IsWarm { get; }

        /// <summary>
        /// Warm the telemetry subsystem.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WarmupAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Capture a full telemetry snapshot.
        /// </summary>
        /// <param name="requestOptions">Section selection options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Telemetry snapshot.</returns>
        Task<TelemetrySnapshot> GetSnapshotAsync(TelemetryRequestOptions requestOptions, CancellationToken cancellationToken);
    }
}
