namespace RigMonitor.Telemetry.Gpu.Amd
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Telemetry.Gpu.Prometheus;

    /// <summary>
    /// Maps AMD Device Metrics Exporter Prometheus output to AMD devices.
    /// Metric names are matched through a table so a renamed metric is a one-line change.
    /// </summary>
    public static class AmdMetricsExporterParser
    {
        private static readonly Dictionary<string, string> _MetricFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "gpu_gfx_activity", "utilization" },
            { "gpu_umc_activity", "memoryActivity" },
            { "gpu_mm_activity", "multimediaActivity" },
            { "gpu_used_vram", "vramUsed" },
            { "gpu_total_vram", "vramTotal" },
            { "gpu_free_vram", "vramFree" },
            { "gpu_used_gtt", "gttUsed" },
            { "gpu_total_gtt", "gttTotal" },
            { "gpu_edge_temperature", "edgeTemperature" },
            { "gpu_junction_temperature", "hotspotTemperature" },
            { "gpu_memory_temperature", "memoryTemperature" },
            { "gpu_package_power", "power" },
            { "gpu_power_usage", "powerFallback" },
            { "gpu_average_package_power", "powerFallback" },
            { "gpu_clock", "clock" },
            { "gpu_ecc_uncorrect_total", "eccUncorrectable" },
            { "gpu_ecc_correct_total", "eccCorrectable" }
        };

        /// <summary>
        /// Whether the text contains AMD exporter metrics.
        /// </summary>
        /// <param name="metrics">Exposition text.</param>
        /// <returns>True when AMD metrics are present.</returns>
        public static bool ContainsAmdMetrics(string? metrics)
        {
            return !String.IsNullOrWhiteSpace(metrics)
                && (metrics.Contains("gpu_gfx_activity", StringComparison.OrdinalIgnoreCase)
                    || metrics.Contains("gpu_used_vram", StringComparison.OrdinalIgnoreCase)
                    || metrics.Contains("gpu_edge_temperature", StringComparison.OrdinalIgnoreCase)
                    || metrics.Contains("gpu_junction_temperature", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Parse exporter output.
        /// </summary>
        /// <param name="metrics">Exposition text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Devices (empty when no AMD metrics are present).</returns>
        public static List<AmdGpuDeviceTelemetry> Parse(string? metrics, CancellationToken cancellationToken = default)
        {
            Dictionary<string, AmdGpuDeviceTelemetry> devices = new Dictionary<string, AmdGpuDeviceTelemetry>(StringComparer.Ordinal);
            Dictionary<string, double> vramFree = new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, double> fallbackPower = new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, double> gttUsed = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (PrometheusSample sample in PrometheusTextParser.Parse(metrics, cancellationToken))
            {
                if (!_MetricFields.TryGetValue(sample.Name, out string? field) || Double.IsNaN(sample.Value) || Double.IsInfinity(sample.Value))
                {
                    continue;
                }

                string gpuId = sample.Label("gpu_id", "gpu", "card");
                string partitionId = sample.Label("gpu_partition_id", "partition_id");
                string key = gpuId + "/" + partitionId;
                AmdGpuDeviceTelemetry device = GetOrCreate(devices, key, sample, gpuId, partitionId);
                double value = sample.Value;

                switch (field)
                {
                    case "utilization":
                        device.Metrics.GpuUtilizationPercent = value;
                        Mark(device, "utilization");
                        break;
                    case "memoryActivity":
                        device.MemoryActivityPercent = value;
                        Mark(device, "memoryActivity");
                        break;
                    case "multimediaActivity":
                        device.MultimediaActivityPercent = value;
                        break;
                    case "vramUsed":
                        device.Metrics.MemoryUsedMegabytes = value;
                        MarkVram(device);
                        break;
                    case "vramTotal":
                        device.Metrics.MemoryTotalMegabytes = value;
                        MarkVram(device);
                        break;
                    case "vramFree":
                        vramFree[key] = value;
                        break;
                    case "gttUsed":
                        gttUsed[key] = value;
                        break;
                    case "gttTotal":
                        device.GttTotalMegabytes = value;
                        Mark(device, "gtt");
                        break;
                    case "edgeTemperature":
                        if (value > 0D)
                        {
                            device.Metrics.TemperatureCelsius = value;
                            Mark(device, "temperature");
                        }

                        break;
                    case "hotspotTemperature":
                        if (value > 0D)
                        {
                            device.HotspotTemperatureCelsius = value;
                            Mark(device, "hotspotTemperature");
                        }

                        break;
                    case "memoryTemperature":
                        if (value > 0D)
                        {
                            device.MemoryTemperatureCelsius = value;
                            Mark(device, "memoryTemperature");
                        }

                        break;
                    case "power":
                        device.Metrics.PowerUsageWatts = value;
                        Mark(device, "power");
                        break;
                    case "powerFallback":
                        fallbackPower[key] = value;
                        break;
                    case "clock":
                        ApplyClock(device, sample);
                        break;
                    case "eccUncorrectable":
                        device.UncorrectableEccErrors = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        Mark(device, "ecc");
                        break;
                    case "eccCorrectable":
                        device.CorrectableEccErrors = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        Mark(device, "ecc");
                        break;
                }
            }

            foreach (KeyValuePair<string, AmdGpuDeviceTelemetry> pair in devices)
            {
                AmdGpuDeviceTelemetry device = pair.Value;
                if (vramFree.TryGetValue(pair.Key, out double free))
                {
                    device.Metrics.MemoryFreeMegabytes = free;
                }
                else if (device.Metrics.MemoryTotalMegabytes > 0D)
                {
                    device.Metrics.MemoryFreeMegabytes = Math.Max(0D, device.Metrics.MemoryTotalMegabytes - device.Metrics.MemoryUsedMegabytes);
                }

                if (!device.Metrics.PowerUsageWatts.HasValue && fallbackPower.TryGetValue(pair.Key, out double power))
                {
                    device.Metrics.PowerUsageWatts = power;
                    Mark(device, "power");
                }

                if (gttUsed.TryGetValue(pair.Key, out double gtt))
                {
                    device.GttUsedMegabytes = gtt;
                }

                if (!device.Metrics.TemperatureCelsius.HasValue && device.HotspotTemperatureCelsius.HasValue)
                {
                    device.Metrics.TemperatureCelsius = device.HotspotTemperatureCelsius;
                    Mark(device, "temperature");
                }

                double? gttTotalBytes = device.GttTotalMegabytes.HasValue ? device.GttTotalMegabytes * 1024D * 1024D : null;
                double? vramTotalBytes = device.Metrics.MemoryTotalMegabytes > 0D ? device.Metrics.MemoryTotalMegabytes * 1024D * 1024D : null;
                device.UnifiedMemory = AmdSysfsBackend.IsApu(vramTotalBytes, gttTotalBytes, device.Model, null);
            }

            return devices.Values
                .OrderBy(device => device.DeviceIndex)
                .ThenBy(device => device.PartitionId ?? String.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private static void ApplyClock(AmdGpuDeviceTelemetry device, PrometheusSample sample)
        {
            string clockType = sample.Label("clock_type", "type").ToUpperInvariant();
            if (clockType.Contains("MEM", StringComparison.Ordinal))
            {
                device.Metrics.MemoryClockMHz = sample.Value;
                Mark(device, "memoryClock");
            }
            else if (clockType.Contains("SYSTEM", StringComparison.Ordinal) || clockType.Contains("GFX", StringComparison.Ordinal) || clockType.Contains("SCLK", StringComparison.Ordinal))
            {
                device.Metrics.SmClockMHz = sample.Value;
                Mark(device, "smClock");
            }
        }

        private static AmdGpuDeviceTelemetry GetOrCreate(
            Dictionary<string, AmdGpuDeviceTelemetry> devices,
            string key,
            PrometheusSample sample,
            string gpuId,
            string partitionId)
        {
            if (devices.TryGetValue(key, out AmdGpuDeviceTelemetry? existing))
            {
                return existing;
            }

            Int32.TryParse(gpuId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index);
            string computePartition = sample.Label("gpu_compute_partition_type", "compute_partition");
            string memoryPartition = sample.Label("gpu_memory_partition_type", "memory_partition");
            AmdGpuDeviceTelemetry device = new AmdGpuDeviceTelemetry
            {
                DeviceIndex = index,
                Source = GpuTelemetrySourceEnum.AmdMetricsExporter,
                Uuid = sample.Label("gpu_uuid", "serial_number", "uuid"),
                BusId = AmdSysfsBackend.NormalizeBusId(sample.Label("pci_bus_id", "bdf")),
                Model = FirstNonEmpty(sample.Label("card_model", "card_series", "product_name"), "AMD GPU"),
                DriverVersion = sample.Label("driver_version"),
                PartitionId = String.IsNullOrWhiteSpace(partitionId) ? null : partitionId,
                ComputePartition = String.IsNullOrWhiteSpace(computePartition) ? null : computePartition,
                MemoryPartition = String.IsNullOrWhiteSpace(memoryPartition) ? null : memoryPartition
            };

            devices[key] = device;
            return device;
        }

        private static void Mark(AmdGpuDeviceTelemetry device, string field)
        {
            device.FieldSources[field] = GpuTelemetrySourceEnum.AmdMetricsExporter;
        }

        private static void MarkVram(AmdGpuDeviceTelemetry device)
        {
            device.Metrics.MemorySource = "vram";
            device.Metrics.MemoryShared = false;
            Mark(device, "memory");
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
