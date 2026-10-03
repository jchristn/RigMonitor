namespace RigMonitor.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Database.Interfaces;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;

    /// <summary>
    /// Decorator that instruments every telemetry history database operation with a client span
    /// ("sqlite create", "sqlite rollup", ...), integration counters and latency histograms, and the
    /// persisted/deleted sample domain counters. Behavior of the inner implementation is unchanged.
    /// Thread safety: as thread safe as the inner implementation.
    /// </summary>
    public class InstrumentedTelemetryHistoryMethods : ITelemetryHistoryMethods
    {
        #region Public-Members

        /// <summary>
        /// The wrapped implementation.
        /// </summary>
        public ITelemetryHistoryMethods Inner
        {
            get
            {
                return _Inner;
            }
        }

        #endregion

        #region Private-Members

        private readonly ITelemetryHistoryMethods _Inner;
        private readonly string _Service;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the decorator.
        /// </summary>
        /// <param name="inner">Implementation to wrap.</param>
        /// <param name="service">Integration service label, for example sqlite.</param>
        /// <exception cref="ArgumentNullException">Thrown when inner is null.</exception>
        public InstrumentedTelemetryHistoryMethods(ITelemetryHistoryMethods inner, string service)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _Service = String.IsNullOrWhiteSpace(service) ? RigMonitorTelemetryNames.ServiceSqlite : service;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<TelemetrySampleDetail> CreateAsync(TelemetrySnapshot snapshot, CancellationToken token = default)
        {
            TelemetrySampleDetail result = await RunAsync("create", call => _Inner.CreateAsync(snapshot, token), token).ConfigureAwait(false);
            RigMonitorInstrumentation.RecordSamplesPersisted(1L);
            return result;
        }

        /// <inheritdoc />
        public Task<TelemetrySampleRecord?> ReadAsync(string id, CancellationToken token = default)
        {
            return RunAsync("read", call => _Inner.ReadAsync(id, token), token);
        }

        /// <inheritdoc />
        public Task<TelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token = default)
        {
            return RunAsync("read_detail", call => _Inner.ReadDetailAsync(id, token), token);
        }

        /// <inheritdoc />
        public Task<EnumerationResult<TelemetrySampleRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            return RunAsync("enumerate", call => _Inner.EnumerateAsync(query, token), token);
        }

        /// <inheritdoc />
        public Task<TelemetryHistorySearchResult> SearchAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default)
        {
            return RunAsync("search", call => _Inner.SearchAsync(filter, token), token);
        }

        /// <inheritdoc />
        public Task<TelemetryRollupResult> RollupAsync(TelemetryRollupRequest request, CancellationToken token = default)
        {
            return RunAsync("rollup", call => _Inner.RollupAsync(request, token), token);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            bool deleted = await RunAsync("delete", call => _Inner.DeleteAsync(id, token), token).ConfigureAwait(false);
            if (deleted) RigMonitorInstrumentation.RecordSamplesDeleted("api", 1L);
            return deleted;
        }

        /// <inheritdoc />
        public async Task<long> DeleteBulkAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default)
        {
            long deleted = await RunAsync("delete_bulk", call => _Inner.DeleteBulkAsync(filter, token), token).ConfigureAwait(false);
            RigMonitorInstrumentation.RecordSamplesDeleted("api_bulk", deleted);
            return deleted;
        }

        /// <inheritdoc />
        public async Task<long> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken token = default)
        {
            long deleted = await RunAsync("prune", call => _Inner.DeleteOlderThanAsync(cutoffUtc, token), token).ConfigureAwait(false);
            RigMonitorInstrumentation.RecordSamplesDeleted("retention", deleted);
            return deleted;
        }

        #endregion

        #region Private-Methods

        private async Task<T> RunAsync<T>(string operation, Func<IntegrationCall, Task<T>> action, CancellationToken token)
        {
            using (IntegrationCall call = IntegrationCall.Start(_Service, operation))
            {
                call.SetTag("db.system.name", _Service);
                call.SetTag("db.operation.name", operation);

                try
                {
                    T result = await action(call).ConfigureAwait(false);
                    call.Succeed();
                    return result;
                }
                catch (Exception exception)
                {
                    call.Fail(exception, token);
                    throw;
                }
            }
        }

        #endregion
    }
}
