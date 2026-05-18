namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Cross-platform memory provider contract.
    /// </summary>
    public interface IMemoryInfoProvider
    {
        /// <summary>
        /// Capture memory telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Memory telemetry.</returns>
        Task<MemoryTelemetry> GetMemoryTelemetryAsync(CancellationToken cancellationToken);
    }
}
