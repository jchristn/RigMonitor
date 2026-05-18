namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Ollama client contract.
    /// </summary>
    public interface IOllamaClient
    {
        /// <summary>
        /// Determine whether Ollama is available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when reachable.</returns>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Capture Ollama telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ollama telemetry when reachable.</returns>
        Task<OllamaTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken);
    }
}
