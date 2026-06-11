namespace Test.Xunit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;

    /// <summary>
    /// Fake telemetry service for persistence worker tests.
    /// </summary>
    internal class FakeTelemetryService : ITelemetryService
    {
        private readonly Func<TelemetrySnapshot> _SnapshotFactory;
        private int _SnapshotCount = 0;

        /// <summary>
        /// Instantiate the fake service.
        /// </summary>
        /// <param name="snapshotFactory">Snapshot factory.</param>
        public FakeTelemetryService(Func<TelemetrySnapshot> snapshotFactory)
        {
            _SnapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
        }

        /// <inheritdoc />
        public bool IsWarm { get; private set; } = true;

        /// <summary>
        /// Number of snapshots requested.
        /// </summary>
        public int SnapshotCount
        {
            get
            {
                return _SnapshotCount;
            }
        }

        /// <inheritdoc />
        public Task WarmupAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsWarm = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<TelemetrySnapshot> GetSnapshotAsync(TelemetryRequestOptions requestOptions, CancellationToken cancellationToken)
        {
            if (requestOptions == null) throw new ArgumentNullException(nameof(requestOptions));
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _SnapshotCount);
            return Task.FromResult(_SnapshotFactory());
        }
    }
}
