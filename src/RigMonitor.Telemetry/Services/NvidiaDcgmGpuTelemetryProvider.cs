namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// NVIDIA GPU telemetry provider backed by DCGM exporter.
    /// </summary>
    public class NvidiaDcgmGpuTelemetryProvider : IGpuTelemetryProvider
    {
        private readonly IDcgmExporterClient _Client;
        private readonly TelemetrySettings _Settings;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="client">DCGM client.</param>
        public NvidiaDcgmGpuTelemetryProvider(TelemetrySettings settings, IDcgmExporterClient client)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (client == null) throw new ArgumentNullException(nameof(client));

            _Settings = settings;
            _Client = client;
        }

        /// <summary>
        /// Capture GPU telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>GPU telemetry when available.</returns>
        public async Task<GpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            string? metrics = await _Client.TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(metrics))
            {
                return null;
            }

            Dictionary<int, GpuDeviceTelemetry> devices = new Dictionary<int, GpuDeviceTelemetry>();
            string[] lines = metrics.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int valueSeparator = line.LastIndexOf(' ');
                if (valueSeparator < 0)
                {
                    continue;
                }

                string left = line.Substring(0, valueSeparator).Trim();
                string valueText = line.Substring(valueSeparator + 1).Trim();
                if (!Double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    continue;
                }

                string metricName = left;
                Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                int braceStart = left.IndexOf('{');
                if (braceStart >= 0)
                {
                    metricName = left.Substring(0, braceStart);
                    int braceEnd = left.LastIndexOf('}');
                    if (braceEnd > braceStart)
                    {
                        string labelsText = left.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        labels = ParseLabels(labelsText);
                    }
                }

                int deviceIndex = ParseDeviceIndex(labels);
                GpuDeviceTelemetry device = GetOrCreateDevice(devices, deviceIndex, labels);
                ApplyMetric(metricName, device, labels, value);
            }

            if (devices.Count < 1)
            {
                return null;
            }

            GpuTelemetry telemetry = new GpuTelemetry
            {
                Vendor = "NVIDIA",
                ExporterEndpoint = _Settings.DcgmExporterUrl,
                Devices = devices.Values.OrderBy(node => node.DeviceIndex).ToList()
            };

            return telemetry;
        }

        private static void ApplyMetric(string metricName, GpuDeviceTelemetry device, Dictionary<string, string> labels, double value)
        {
            if (String.Equals(metricName, "DCGM_FI_DEV_GPU_UTIL", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.GpuUtilizationPercent = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_FB_USED", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.MemoryUsedMegabytes = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_FB_FREE", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.MemoryFreeMegabytes = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_GPU_TEMP", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.TemperatureCelsius = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_POWER_USAGE", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.PowerUsageWatts = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_SM_CLOCK", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.SmClockMHz = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_MEM_CLOCK", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.MemoryClockMHz = value;
            }
            else if (String.Equals(metricName, "DCGM_FI_DEV_XID_ERRORS", StringComparison.OrdinalIgnoreCase))
            {
                device.Metrics.XidErrors = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            string driverVersion = ReadLabel(labels, "driver_version", "DriverVersion");
            if (!String.IsNullOrWhiteSpace(driverVersion))
            {
                device.DriverVersion = driverVersion;
            }
        }

        private static GpuDeviceTelemetry GetOrCreateDevice(
            Dictionary<int, GpuDeviceTelemetry> devices,
            int deviceIndex,
            Dictionary<string, string> labels)
        {
            if (devices.TryGetValue(deviceIndex, out GpuDeviceTelemetry? existing))
            {
                return existing;
            }

            GpuDeviceTelemetry device = new GpuDeviceTelemetry
            {
                DeviceIndex = deviceIndex,
                Uuid = ReadLabel(labels, "UUID", "uuid"),
                BusId = ReadLabel(labels, "pci_bus_id", "busId"),
                Model = ReadLabel(labels, "modelName", "device", "gpu_name"),
                DriverVersion = ReadLabel(labels, "driver_version", "DriverVersion"),
                MigProfile = ReadLabel(labels, "mig_profile", "GPU_I_PROFILE", "GPU_I_PROFILE_ID")
            };

            devices[deviceIndex] = device;
            return device;
        }

        private static int ParseDeviceIndex(Dictionary<string, string> labels)
        {
            string rawValue = ReadLabel(labels, "gpu", "device_id", "index");
            if (String.IsNullOrWhiteSpace(rawValue))
            {
                return 0;
            }

            if (Int32.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return Math.Max(0, index);
            }

            return 0;
        }

        private static string ReadLabel(Dictionary<string, string> labels, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (labels.TryGetValue(candidate, out string? value) && !String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return String.Empty;
        }

        private static Dictionary<string, string> ParseLabels(string input)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> segments = new List<string>();
            bool inQuotes = false;
            int start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (input[i] == ',' && !inQuotes)
                {
                    segments.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }

            segments.Add(input.Substring(start));

            foreach (string segment in segments)
            {
                string[] pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2)
                {
                    continue;
                }

                string value = pair[1].Trim().Trim('"');
                labels[pair[0].Trim()] = value;
            }

            return labels;
        }
    }
}
