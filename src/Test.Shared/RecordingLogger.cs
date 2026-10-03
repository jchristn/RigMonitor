namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// ILogger that records every entry with the trace id active when it was written.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class RecordingLogger : ILogger
    {
        #region Public-Members

        /// <summary>
        /// Recorded entries formatted as "level|traceId|message".
        /// </summary>
        public List<string> Entries
        {
            get
            {
                return _Entries.ToList();
            }
        }

        #endregion

        #region Private-Members

        private readonly ConcurrentQueue<string> _Entries = new ConcurrentQueue<string>();

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string traceId = Activity.Current != null ? Activity.Current.TraceId.ToHexString() : "-";
            _Entries.Enqueue(logLevel + "|" + traceId + "|" + formatter(state, exception));
        }

        #endregion
    }
}
