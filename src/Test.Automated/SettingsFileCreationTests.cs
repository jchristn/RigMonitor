namespace Test.Automated
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Server.Services;

    /// <summary>
    /// Settings file creation tests.
    /// </summary>
    public class SettingsFileCreationTests
    {
        /// <summary>
        /// Verify that the settings manager creates a missing file with defaults.
        /// </summary>
        [Fact]
        public async Task ShouldCreateSettingsFileWhenMissing()
        {
            string directory = Path.Combine(Path.GetTempPath(), "RigMonitorTests", Guid.NewGuid().ToString("N"));
            string settingsFile = Path.Combine(directory, "rigmonitor.json");

            try
            {
                Assert.False(File.Exists(settingsFile));
                RigMonitor.Core.Settings.RigMonitorSettings settings = await SettingsManager.LoadAsync(settingsFile, CancellationToken.None);
                Assert.True(File.Exists(settingsFile));
                Assert.Equal("localhost", settings.Webserver.Hostname);
                Assert.Equal("http://localhost:11434", settings.Telemetry.OllamaBaseUrl);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
