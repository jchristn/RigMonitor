namespace RigMonitor.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Database.Interfaces;

    /// <summary>
    /// Base class for database drivers.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable
    {
        /// <summary>
        /// Telemetry history methods.
        /// </summary>
        public abstract ITelemetryHistoryMethods TelemetryHistory { get; }

        /// <summary>
        /// Initialize the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Dispose driver resources.
        /// </summary>
        public abstract void Dispose();
    }
}
