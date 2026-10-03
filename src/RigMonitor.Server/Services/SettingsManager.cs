namespace RigMonitor.Server.Services
{
    using System;
    using System.Globalization;
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

            string normalizedJson = RigMonitorJsonSerializer.Serialize(settings, true);
            if (!String.Equals(fileContents, normalizedJson, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(fullPath, normalizedJson, cancellationToken).ConfigureAwait(false);
            }

            // Applied after the file is written so environment-specific values are never persisted.
            ApplyEnvironmentOverrides(settings, null);
            return settings;
        }

        /// <summary>
        /// Apply RIGMONITOR_OBSERVABILITY_* environment variable overrides to the observability settings.
        /// Unset or unparseable variables leave the configured value unchanged. See TELEMETRY.md for the list.
        /// </summary>
        /// <param name="settings">Settings to update.</param>
        /// <param name="environment">Optional variable lookup; defaults to the process environment.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static void ApplyEnvironmentOverrides(RigMonitorSettings settings, Func<string, string?>? environment)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Func<string, string?> lookup = environment ?? Environment.GetEnvironmentVariable;
            ObservabilitySettings observability = settings.Observability ?? new ObservabilitySettings();
            settings.Observability = observability;

            bool? enabled = ReadBool(lookup, "RIGMONITOR_OBSERVABILITY_ENABLED");
            if (enabled.HasValue) observability.Enabled = enabled.Value;

            string? serviceName = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_SERVICE_NAME");
            if (serviceName != null) observability.ServiceName = serviceName;

            bool? otlpEnabled = ReadBool(lookup, "RIGMONITOR_OBSERVABILITY_OTLP_ENABLED");
            if (otlpEnabled.HasValue) observability.OtlpEnabled = otlpEnabled.Value;

            string? otlpEndpoint = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_OTLP_ENDPOINT");
            if (otlpEndpoint != null) observability.OtlpEndpoint = otlpEndpoint;

            string? otlpProtocol = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_OTLP_PROTOCOL");
            if (otlpProtocol != null) observability.OtlpProtocol = otlpProtocol;

            bool? prometheusEnabled = ReadBool(lookup, "RIGMONITOR_OBSERVABILITY_PROMETHEUS_ENABLED");
            if (prometheusEnabled.HasValue) observability.PrometheusEnabled = prometheusEnabled.Value;

            string? prometheusHostname = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_PROMETHEUS_HOSTNAME");
            if (prometheusHostname != null) observability.PrometheusHostname = prometheusHostname;

            string? prometheusPort = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_PROMETHEUS_PORT");
            if (prometheusPort != null && Int32.TryParse(prometheusPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            {
                observability.PrometheusPort = port;
            }

            bool? lokiEnabled = ReadBool(lookup, "RIGMONITOR_OBSERVABILITY_LOKI_ENABLED");
            if (lokiEnabled.HasValue) observability.LokiEnabled = lokiEnabled.Value;

            string? lokiEndpoint = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_LOKI_ENDPOINT");
            if (lokiEndpoint != null) observability.LokiEndpoint = lokiEndpoint;

            string? lokiTenant = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_LOKI_TENANT_ID");
            if (lokiTenant != null) observability.LokiTenantId = lokiTenant;

            string? samplingRatio = ReadString(lookup, "RIGMONITOR_OBSERVABILITY_TRACE_SAMPLING_RATIO");
            if (samplingRatio != null && Double.TryParse(samplingRatio, NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio))
            {
                observability.TraceSamplingRatio = ratio;
            }
        }

        private static string? ReadString(Func<string, string?> lookup, string name)
        {
            string? value = lookup(name);
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool? ReadBool(Func<string, string?> lookup, string name)
        {
            string? value = ReadString(lookup, name);
            if (value == null) return null;
            if (Boolean.TryParse(value, out bool parsed)) return parsed;
            if (value == "1") return true;
            if (value == "0") return false;
            return null;
        }
    }
}
