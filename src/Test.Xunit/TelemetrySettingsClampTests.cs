namespace Test.Xunit
{
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Telemetry settings clamp tests.
    /// </summary>
    public class TelemetrySettingsClampTests
    {
        /// <summary>
        /// Verify timeout and warmup values are clamped into valid ranges.
        /// </summary>
        [Fact]
        public void ShouldClampTelemetrySettings()
        {
            TelemetrySettings settings = new TelemetrySettings
            {
                RequestTimeoutMs = 10,
                WarmupDelayMs = 1000000
            };

            Assert.Equal(500, settings.RequestTimeoutMs);
            Assert.Equal(60000, settings.WarmupDelayMs);
        }
    }
}
