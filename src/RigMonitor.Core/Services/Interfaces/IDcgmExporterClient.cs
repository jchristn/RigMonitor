namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// DCGM exporter client contract.
    /// </summary>
    public interface IDcgmExporterClient
    {
        /// <summary>
        /// Retrieve raw Prometheus metrics from the configured exporter.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Metrics text on success, or null when unavailable.</returns>
        Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken);
    }
}
