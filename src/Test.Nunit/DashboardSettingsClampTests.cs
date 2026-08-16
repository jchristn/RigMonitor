namespace Test.Nunit
{
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Dashboard settings clamp tests.
    /// </summary>
    public class DashboardSettingsClampTests
    {
        /// <summary>
        /// Verify the dashboard refresh interval is clamped up to the minimum.
        /// </summary>
        [Test]
        public void ShouldClampDashboardRefreshInterval()
        {
            DashboardSettings settings = new DashboardSettings
            {
                AutoRefreshIntervalMs = 10
            };

            Assert.That(settings.AutoRefreshIntervalMs, Is.EqualTo(1000));
        }

        /// <summary>
        /// Verify the dashboard refresh interval is clamped down to the maximum.
        /// </summary>
        [Test]
        public void ShouldClampDashboardRefreshIntervalToMaximum()
        {
            DashboardSettings settings = new DashboardSettings
            {
                AutoRefreshIntervalMs = 999999999
            };

            Assert.That(settings.AutoRefreshIntervalMs, Is.EqualTo(3600000));
        }

        /// <summary>
        /// Verify an in-range dashboard refresh interval is preserved unchanged.
        /// </summary>
        [Test]
        public void ShouldPreserveInRangeDashboardRefreshInterval()
        {
            DashboardSettings settings = new DashboardSettings
            {
                AutoRefreshIntervalMs = 5000
            };

            Assert.That(settings.AutoRefreshIntervalMs, Is.EqualTo(5000));
        }

        /// <summary>
        /// Verify a freshly constructed dashboard settings object exposes its defaults.
        /// </summary>
        [Test]
        public void ShouldExposeDashboardDefaults()
        {
            DashboardSettings settings = new DashboardSettings();

            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.AutoRefreshIntervalMs, Is.EqualTo(5000));
        }
    }
}
