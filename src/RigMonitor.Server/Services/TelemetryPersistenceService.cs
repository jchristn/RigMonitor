namespace RigMonitor.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Database;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Background telemetry persistence and retention service.
    /// </summary>
    public class TelemetryPersistenceService : IDisposable
    {
        private readonly PersistenceSettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly ITelemetryService _TelemetryService;
        private readonly AppLogger _Logger;
        private readonly TimeProvider _TimeProvider;
        private readonly object _StatusLock = new object();
        private CancellationTokenSource? _CancellationTokenSource = null;
        private Task? _CollectionTask = null;
        private Task? _RetentionTask = null;
        private DateTime? _LastAttemptUtc = null;
        private DateTime? _LastSuccessUtc = null;
        private DateTime? _NextCollectionUtc = null;
        private string? _LastError = null;
        private int _CollectionRunning = 0;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="settings">Persistence settings.</param>
        /// <param name="database">Database driver.</param>
        /// <param name="telemetryService">Telemetry service.</param>
        /// <param name="logger">Application logger.</param>
        /// <param name="timeProvider">Optional time provider.</param>
        public TelemetryPersistenceService(
            PersistenceSettings settings,
            DatabaseDriverBase database,
            ITelemetryService telemetryService,
            AppLogger logger,
            TimeProvider? timeProvider = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _TimeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Start background persistence loops.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public Task StartAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!_Settings.Enabled)
            {
                _Logger.Info("Telemetry persistence disabled.");
                return Task.CompletedTask;
            }

            if (_CancellationTokenSource != null)
            {
                return Task.CompletedTask;
            }

            _CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _CollectionTask = Task.Run(() => RunCollectionLoopAsync(_CancellationTokenSource.Token), CancellationToken.None);
            _RetentionTask = Task.Run(() => RunRetentionLoopAsync(_CancellationTokenSource.Token), CancellationToken.None);
            _Logger.Info("Telemetry persistence enabled for hostname " + _Settings.Hostname + " using " + _Settings.Database.Type + ".");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop background persistence loops.
        /// </summary>
        public async Task StopAsync()
        {
            if (_CancellationTokenSource == null)
            {
                return;
            }

            _CancellationTokenSource.Cancel();
            try
            {
                if (_CollectionTask != null)
                {
                    await _CollectionTask.ConfigureAwait(false);
                }

                if (_RetentionTask != null)
                {
                    await _RetentionTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _CancellationTokenSource.Dispose();
                _CancellationTokenSource = null;
                _CollectionTask = null;
                _RetentionTask = null;
            }
        }

        /// <summary>
        /// Get current persistence status.
        /// </summary>
        /// <returns>Status payload.</returns>
        public TelemetryPersistenceStatus GetStatus()
        {
            lock (_StatusLock)
            {
                return new TelemetryPersistenceStatus
                {
                    Enabled = _Settings.Enabled,
                    Hostname = _Settings.Hostname,
                    CollectionIntervalMs = _Settings.CollectionIntervalMs,
                    RetentionDays = _Settings.RetentionDays,
                    DatabaseType = _Settings.Database.Type.ToString(),
                    DatabaseFilename = _Settings.Database.Filename,
                    LastAttemptUtc = _LastAttemptUtc,
                    LastSuccessUtc = _LastSuccessUtc,
                    NextCollectionUtc = _NextCollectionUtc,
                    LastError = _LastError
                };
            }
        }

        /// <summary>
        /// Dispose background resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            StopAsync().GetAwaiter().GetResult();
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        private async Task RunCollectionLoopAsync(CancellationToken token)
        {
            try
            {
                _Logger.Debug("Automated telemetry collection loop starting immediate collection for hostname " + _Settings.Hostname + ".");
                await CollectOnceAsync(token).ConfigureAwait(false);

                using (PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_Settings.CollectionIntervalMs)))
                {
                    while (!token.IsCancellationRequested)
                    {
                        DateTime nextCollectionUtc = _TimeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(_Settings.CollectionIntervalMs);
                        SetNextCollection(nextCollectionUtc);
                        _Logger.Debug("Next automated telemetry collection scheduled for " + nextCollectionUtc.ToString("O") + ".");
                        if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                        {
                            break;
                        }

                        await CollectOnceAsync(token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                _Logger.Warn("Telemetry persistence collection loop stopped unexpectedly: " + exception.Message);
            }
        }

        private async Task CollectOnceAsync(CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref _CollectionRunning, 1, 0) != 0)
            {
                _Logger.Warn("Skipping telemetry persistence collection because the prior collection is still running.");
                return;
            }

            DateTime attemptUtc = _TimeProvider.GetUtcNow().UtcDateTime;
            lock (_StatusLock)
            {
                _LastAttemptUtc = attemptUtc;
            }

            try
            {
                _Logger.Debug("Automated telemetry collection started at " + attemptUtc.ToString("O") + " for hostname " + _Settings.Hostname + ".");
                TelemetrySnapshot snapshot = await _TelemetryService.GetSnapshotAsync(TelemetryRequestOptions.All(), token).ConfigureAwait(false);
                TelemetrySampleDetail sample = await _Database.TelemetryHistory.CreateAsync(snapshot, token).ConfigureAwait(false);

                lock (_StatusLock)
                {
                    _LastSuccessUtc = attemptUtc;
                    _LastError = null;
                }

                _Logger.Debug("Automated telemetry collection persisted sample " + sample.Id + " collected at " + snapshot.CollectedUtc.ToString("O") + " for hostname " + _Settings.Hostname + ".");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                _Logger.Warn("Failed to persist telemetry sample: " + exception.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _CollectionRunning, 0);
            }
        }

        private async Task RunRetentionLoopAsync(CancellationToken token)
        {
            try
            {
                await PruneExpiredAsync(token).ConfigureAwait(false);

                using (PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(_Settings.PruneIntervalMinutes)))
                {
                    while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                    {
                        await PruneExpiredAsync(token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _Logger.Warn("Telemetry persistence retention loop stopped unexpectedly: " + exception.Message);
            }
        }

        private async Task PruneExpiredAsync(CancellationToken token)
        {
            DateTime cutoffUtc = _TimeProvider.GetUtcNow().UtcDateTime.AddDays(-_Settings.RetentionDays);
            long deleted = await _Database.TelemetryHistory.DeleteOlderThanAsync(cutoffUtc, token).ConfigureAwait(false);
            if (deleted > 0L)
            {
                _Logger.Info("Pruned " + deleted + " telemetry sample(s) older than " + cutoffUtc.ToString("O") + ".");
            }
        }

        private void SetError(string error)
        {
            lock (_StatusLock)
            {
                _LastError = error;
            }
        }

        private void SetNextCollection(DateTime nextCollectionUtc)
        {
            lock (_StatusLock)
            {
                _NextCollectionUtc = nextCollectionUtc;
            }
        }
    }
}
