namespace RigMonitor.Telemetry.Gpu.Nvidia
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Prometheus;

    /// <summary>
    /// NVIDIA GPU telemetry provider backed by DCGM exporter.
    /// </summary>
    public class NvidiaDcgmGpuTelemetryProvider : INvidiaGpuTelemetryProvider
    {
        private const string _FramebufferMemorySource = "framebuffer";
        private readonly IPrometheusTextClient _Client;
        private readonly TelemetrySettings _Settings;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="client">DCGM client.</param>
        public NvidiaDcgmGpuTelemetryProvider(TelemetrySettings settings, IPrometheusTextClient client)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Vendor served by this provider.
        /// </summary>
        public GpuVendorEnum Vendor
        {
            get
            {
                return GpuVendorEnum.Nvidia;
            }
        }

        /// <summary>
        /// Probe the DCGM exporter.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Probe result.</returns>
        public async Task<GpuProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            if (!_Settings.NvidiaEnabled)
            {
                return GpuProbeResult.Unavailable(GpuVendorEnum.Nvidia, "NVIDIA telemetry is disabled in settings.");
            }

            string? metrics = await _Client.TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            bool available = !String.IsNullOrWhiteSpace(metrics)
                && (metrics.Contains("DCGM_FI_DEV_GPU_UTIL", StringComparison.OrdinalIgnoreCase)
                    || metrics.Contains("DCGM_FI_DEV_FB_USED", StringComparison.OrdinalIgnoreCase)
                    || metrics.Contains("DCGM_FI_DEV_GPU_TEMP", StringComparison.OrdinalIgnoreCase));

            return new GpuProbeResult
            {
                Vendor = GpuVendorEnum.Nvidia,
                Available = available,
                Source = available ? GpuTelemetrySourceEnum.DcgmExporter : null,
                Endpoint = _Client.Endpoint,
                Detail = available ? null : "DCGM exporter is unreachable or does not emit DCGM_FI_DEV metrics."
            };
        }

        /// <summary>
        /// Capture NVIDIA telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>NVIDIA section when available.</returns>
        public async Task<NvidiaGpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            string? metrics = await _Client.TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            return Parse(metrics, _Client.Endpoint, cancellationToken);
        }

        /// <summary>
        /// Parse DCGM exporter text into the NVIDIA section.
        /// </summary>
        /// <param name="metrics">Exposition text.</param>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>NVIDIA section, or null when no DCGM devices are present.</returns>
        public static NvidiaGpuTelemetry? Parse(string? metrics, string endpoint, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(metrics))
            {
                return null;
            }

            Dictionary<string, NvidiaGpuDeviceTelemetry> devices = new Dictionary<string, NvidiaGpuDeviceTelemetry>(StringComparer.Ordinal);
            foreach (PrometheusSample sample in PrometheusTextParser.Parse(metrics, cancellationToken))
            {
                if (!sample.Name.StartsWith("DCGM_FI_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                NvidiaGpuDeviceTelemetry device = GetOrCreateDevice(devices, sample);
                ApplyMetric(sample, device);
            }

            if (devices.Count < 1)
            {
                return null;
            }

            return new NvidiaGpuTelemetry
            {
                Source = GpuTelemetrySourceEnum.DcgmExporter,
                Endpoint = endpoint ?? String.Empty,
                Devices = devices.Values
                    .OrderBy(device => device.DeviceIndex)
                    .ThenBy(device => device.MigProfile ?? String.Empty, StringComparer.Ordinal)
                    .ToList()
            };
        }

        /// <summary>
        /// Whether an NVIDIA model shares memory with the host (GB10 / DGX Spark).
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <returns>True for unified-memory parts.</returns>
        public static bool IsUnifiedMemoryModel(string? model)
        {
            string value = model ?? String.Empty;
            return value.IndexOf("GB10", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("DGX Spark", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplyMetric(PrometheusSample sample, NvidiaGpuDeviceTelemetry device)
        {
            double value = sample.Value;
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                return;
            }

            switch (sample.Name.ToUpperInvariant())
            {
                case "DCGM_FI_DEV_GPU_UTIL":
                    device.Metrics.GpuUtilizationPercent = value;
                    break;
                case "DCGM_FI_DEV_FB_USED":
                    device.Metrics.MemoryUsedMegabytes = value;
                    MarkFramebuffer(device);
                    break;
                case "DCGM_FI_DEV_FB_FREE":
                    device.Metrics.MemoryFreeMegabytes = value;
                    MarkFramebuffer(device);
                    break;
                case "DCGM_FI_DEV_FB_TOTAL":
                    device.Metrics.MemoryTotalMegabytes = value;
                    MarkFramebuffer(device);
                    break;
                case "DCGM_FI_DEV_GPU_TEMP":
                    device.Metrics.TemperatureCelsius = value;
                    break;
                case "DCGM_FI_DEV_MEMORY_TEMP":
                    device.MemoryTemperatureCelsius = value;
                    break;
                case "DCGM_FI_DEV_POWER_USAGE":
                    device.Metrics.PowerUsageWatts = value;
                    break;
                case "DCGM_FI_DEV_SM_CLOCK":
                    device.Metrics.SmClockMHz = value;
                    break;
                case "DCGM_FI_DEV_MEM_CLOCK":
                    device.Metrics.MemoryClockMHz = value;
                    break;
                case "DCGM_FI_DEV_MEM_COPY_UTIL":
                    device.MemoryCopyUtilizationPercent = value;
                    break;
                case "DCGM_FI_DEV_XID_ERRORS":
                    device.XidErrors = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    break;
                case "DCGM_FI_DEV_ECC_DBE_VOL_TOTAL":
                    device.UncorrectableEccErrors = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    break;
            }

            string driverVersion = sample.Label("driver_version", "DriverVersion");
            if (!String.IsNullOrWhiteSpace(driverVersion))
            {
                device.DriverVersion = driverVersion;
            }
        }

        private static void MarkFramebuffer(NvidiaGpuDeviceTelemetry device)
        {
            device.Metrics.MemorySource = _FramebufferMemorySource;
            device.Metrics.MemoryShared = false;
        }

        private static NvidiaGpuDeviceTelemetry GetOrCreateDevice(Dictionary<string, NvidiaGpuDeviceTelemetry> devices, PrometheusSample sample)
        {
            int deviceIndex = ParseDeviceIndex(sample);
            string migInstance = sample.Label("GPU_I_ID", "mig_instance_id");
            string key = deviceIndex.ToString(CultureInfo.InvariantCulture) + "/" + migInstance;

            if (devices.TryGetValue(key, out NvidiaGpuDeviceTelemetry? existing))
            {
                return existing;
            }

            string model = sample.Label("modelName", "device", "gpu_name");
            string migProfile = sample.Label("mig_profile", "GPU_I_PROFILE", "GPU_I_PROFILE_ID");
            NvidiaGpuDeviceTelemetry device = new NvidiaGpuDeviceTelemetry
            {
                DeviceIndex = deviceIndex,
                Uuid = sample.Label("UUID", "uuid"),
                BusId = sample.Label("pci_bus_id", "busId"),
                Model = model,
                DriverVersion = sample.Label("driver_version", "DriverVersion"),
                MigProfile = String.IsNullOrWhiteSpace(migProfile) ? null : migProfile,
                UnifiedMemory = IsUnifiedMemoryModel(model)
            };

            devices[key] = device;
            return device;
        }

        private static int ParseDeviceIndex(PrometheusSample sample)
        {
            string rawValue = sample.Label("gpu", "device_id", "index");
            if (Int32.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return Math.Max(0, index);
            }

            return 0;
        }
    }
}
