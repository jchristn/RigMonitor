namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Network throughput sampler contract.
    /// </summary>
    public interface INetworkRateSampler
    {
        /// <summary>
        /// Warm the sampler with an initial sample.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WarmupAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Capture a current network sample.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Network telemetry.</returns>
        Task<NetworkTelemetry> CaptureAsync(CancellationToken cancellationToken);
    }
}
