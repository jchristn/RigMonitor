namespace Test.Xunit
{
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Telemetry settings clamp tests.
    /// </summary>
    public class TelemetrySettingsClampTests
    {
        /// <summary>
        /// Verify values below the minimum and above the maximum are clamped into valid ranges.
        /// </summary>
        [Fact]
        public void ShouldClampTelemetrySettings()
        {
            TelemetrySettings settings = new TelemetrySettings
            {
                RequestTimeoutMs = 10,
                WarmupDelayMs = 1000000,
                SectionStaleAfterMs = 10
            };

            Assert.Equal(500, settings.RequestTimeoutMs);
            Assert.Equal(60000, settings.WarmupDelayMs);
            Assert.Equal(1000, settings.SectionStaleAfterMs);
        }

        /// <summary>
        /// Verify values already within range are preserved unchanged.
        /// </summary>
        [Fact]
        public void ShouldPreserveInRangeTelemetrySettings()
        {
            TelemetrySettings settings = new TelemetrySettings
            {
                RequestTimeoutMs = 5000,
                WarmupDelayMs = 1000,
                SectionStaleAfterMs = 15000,
                UtilyzeSampleStaleAfterMs = 5000
            };

            Assert.Equal(5000, settings.RequestTimeoutMs);
            Assert.Equal(1000, settings.WarmupDelayMs);
            Assert.Equal(15000, settings.SectionStaleAfterMs);
            Assert.Equal(5000, settings.UtilyzeSampleStaleAfterMs);
        }

        /// <summary>
        /// Verify values above the maximum are clamped down to the upper bound.
        /// </summary>
        [Fact]
        public void ShouldClampTelemetrySettingsToUpperBounds()
        {
            TelemetrySettings settings = new TelemetrySettings
            {
                RequestTimeoutMs = 999999,
                SectionStaleAfterMs = 999999999,
                UtilyzeSampleStaleAfterMs = 999999999
            };

            Assert.Equal(300000, settings.RequestTimeoutMs);
            Assert.Equal(3600000, settings.SectionStaleAfterMs);
            Assert.Equal(3600000, settings.UtilyzeSampleStaleAfterMs);
        }

        /// <summary>
        /// Verify the Utilyze sample staleness threshold is clamped up to its minimum.
        /// </summary>
        [Fact]
        public void ShouldClampUtilyzeSampleStalenessToLowerBound()
        {
            TelemetrySettings settings = new TelemetrySettings
            {
                UtilyzeSampleStaleAfterMs = 1
            };

            Assert.Equal(1000, settings.UtilyzeSampleStaleAfterMs);
        }

        /// <summary>
        /// Verify newly constructed settings expose the documented defaults.
        /// </summary>
        [Fact]
        public void ShouldExposeDefaultTelemetrySettings()
        {
            TelemetrySettings settings = new TelemetrySettings();

            Assert.Equal(5000, settings.RequestTimeoutMs);
            Assert.Equal(1000, settings.WarmupDelayMs);
            Assert.Equal(15000, settings.SectionStaleAfterMs);
            Assert.Equal(5000, settings.UtilyzeSampleStaleAfterMs);
        }
    }
}
