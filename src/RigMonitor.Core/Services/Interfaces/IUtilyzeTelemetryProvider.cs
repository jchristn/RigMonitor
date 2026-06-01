namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Utilyze telemetry provider contract.
    /// </summary>
    public interface IUtilyzeTelemetryProvider
    {
        /// <summary>
        /// Determine whether Utilyze is reachable.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if Utilyze is reachable.</returns>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Capture the latest Utilyze telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Latest Utilyze telemetry when available.</returns>
        Task<UtilyzeTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken);
    }
}
