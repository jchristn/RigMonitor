namespace RigMonitor.Server.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Serialization;

    /// <summary>
    /// Loads and creates settings files.
    /// </summary>
    public static class SettingsManager
    {
        /// <summary>
        /// Load a settings file, creating it when absent.
        /// </summary>
        /// <param name="settingsFile">Settings file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Loaded settings.</returns>
        public static async Task<RigMonitorSettings> LoadAsync(string settingsFile, CancellationToken cancellationToken)
        {
            if (String.IsNullOrWhiteSpace(settingsFile)) throw new ArgumentNullException(nameof(settingsFile));

            string fullPath = Path.GetFullPath(settingsFile);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(fullPath))
            {
                RigMonitorSettings defaults = new RigMonitorSettings();
                string json = RigMonitorJsonSerializer.Serialize(defaults, true);
                await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
            }

            string fileContents = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            RigMonitorSettings? settings = RigMonitorJsonSerializer.Deserialize<RigMonitorSettings>(fileContents);

            if (settings == null)
            {
                throw new InvalidDataException("Unable to deserialize settings file '" + fullPath + "'.");
            }

            return settings;
        }
    }
}
