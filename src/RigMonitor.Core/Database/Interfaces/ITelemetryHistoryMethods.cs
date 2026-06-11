namespace RigMonitor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Telemetry history database methods.
    /// </summary>
    public interface ITelemetryHistoryMethods
    {
        /// <summary>
        /// Create a telemetry history sample.
        /// </summary>
        /// <param name="snapshot">Telemetry snapshot.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created sample detail.</returns>
        Task<TelemetrySampleDetail> CreateAsync(TelemetrySnapshot snapshot, CancellationToken token = default);

        /// <summary>
        /// Read lightweight sample metadata.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sample record or null.</returns>
        Task<TelemetrySampleRecord?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read full sample detail.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sample detail or null.</returns>
        Task<TelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate sample metadata.
        /// </summary>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<TelemetrySampleRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Search sample metadata.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result.</returns>
        Task<TelemetryHistorySearchResult> SearchAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default);

        /// <summary>
        /// Produce bucketized telemetry roll-ups.
        /// </summary>
        /// <param name="request">Roll-up request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Roll-up result.</returns>
        Task<TelemetryRollupResult> RollupAsync(TelemetryRollupRequest request, CancellationToken token = default);

        /// <summary>
        /// Delete a sample by identifier.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a sample was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete samples matching the supplied filter.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted sample count.</returns>
        Task<long> DeleteBulkAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete samples older than the supplied cutoff.
        /// </summary>
        /// <param name="cutoffUtc">Exclusive UTC cutoff.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted sample count.</returns>
        Task<long> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken token = default);
    }
}
