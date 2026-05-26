namespace Test.Shared
{
    using System;

    /// <summary>
    /// Deterministic time provider for tests.
    /// </summary>
    public class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _UtcNow;
        private long _Timestamp = 0L;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="utcNow">Initial current time.</param>
        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _UtcNow = utcNow;
        }

        /// <summary>
        /// Frequency used by timestamps.
        /// </summary>
        public override long TimestampFrequency
        {
            get
            {
                return TimeSpan.TicksPerSecond;
            }
        }

        /// <summary>
        /// Local time zone for the provider.
        /// </summary>
        public override TimeZoneInfo LocalTimeZone
        {
            get
            {
                return TimeZoneInfo.Utc;
            }
        }

        /// <summary>
        /// Get the current UTC time.
        /// </summary>
        /// <returns>Current UTC time.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return _UtcNow;
        }

        /// <summary>
        /// Get the current timestamp.
        /// </summary>
        /// <returns>Current timestamp.</returns>
        public override long GetTimestamp()
        {
            return _Timestamp;
        }

        /// <summary>
        /// Advance the provider by the specified duration.
        /// </summary>
        /// <param name="amount">Amount to advance.</param>
        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount), "Time cannot move backwards.");

            _UtcNow = _UtcNow.Add(amount);
            _Timestamp += amount.Ticks;
        }
    }
}
