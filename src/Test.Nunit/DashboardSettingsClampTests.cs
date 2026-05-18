namespace Test.Nunit
{
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Dashboard settings clamp tests.
    /// </summary>
    public class DashboardSettingsClampTests
    {
        /// <summary>
        /// Verify the dashboard refresh interval is clamped into range.
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
    }
}
