namespace Test.Shared.Fixtures
{
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Shared settings test fixture.
    /// </summary>
    public static class SettingsFixture
    {
        /// <summary>
        /// Create a baseline settings instance.
        /// </summary>
        /// <returns>Settings instance.</returns>
        public static RigMonitorSettings Create()
        {
            return new RigMonitorSettings();
        }
    }
}
