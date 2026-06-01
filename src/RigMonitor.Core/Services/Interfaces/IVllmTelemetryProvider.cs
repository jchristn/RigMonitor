namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// vLLM telemetry provider contract.
    /// </summary>
    public interface IVllmTelemetryProvider
    {
        /// <summary>
        /// Determine whether vLLM metrics are reachable.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if vLLM metrics are reachable.</returns>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Capture vLLM telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>vLLM telemetry when available.</returns>
        Task<VllmTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken);
    }
}
