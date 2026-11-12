namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Client that retrieves Prometheus text exposition from an HTTP endpoint.
    /// </summary>
    public interface IPrometheusTextClient
    {
        /// <summary>
        /// Endpoint URL.
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// Retrieve raw Prometheus metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Metrics text on success, or null when unavailable.</returns>
        Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken);
    }
}
