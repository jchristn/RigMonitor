namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// GPU telemetry provider contract.
    /// </summary>
    public interface IGpuTelemetryProvider
    {
        /// <summary>
        /// Capture GPU telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>GPU telemetry when available.</returns>
        Task<GpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken);
    }
}
