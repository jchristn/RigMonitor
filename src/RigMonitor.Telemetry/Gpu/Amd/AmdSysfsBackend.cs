namespace RigMonitor.Telemetry.Gpu.Amd
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Reads AMD GPUs from the Linux amdgpu driver's sysfs interface.
    /// </summary>
    public class AmdSysfsBackend
    {
        internal const string AmdPciVendorId = "0x1002";
        private static readonly Regex _CardName = new Regex("^card[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly string _DrmRoot;

        /// <summary>
        /// Instantiate the backend.
        /// </summary>
        /// <param name="drmRoot">DRM class root (normally /sys/class/drm).</param>
        public AmdSysfsBackend(string drmRoot)
        {
            _DrmRoot = String.IsNullOrWhiteSpace(drmRoot) ? "/sys/class/drm" : drmRoot;
        }

        /// <summary>
        /// DRM class root.
        /// </summary>
        public string Root
        {
            get
            {
                return _DrmRoot;
            }
        }

        /// <summary>
        /// Whether any AMD GPU is present.
        /// </summary>
        /// <returns>True when at least one amdgpu card exists.</returns>
        public bool HasDevices()
        {
            return EnumerateDeviceDirectories().Count > 0;
        }

        /// <summary>
        /// Read every AMD GPU.
        /// </summary>
        /// <returns>Devices.</returns>
        public List<AmdGpuDeviceTelemetry> ReadDevices()
        {
            List<AmdGpuDeviceTelemetry> devices = new List<AmdGpuDeviceTelemetry>();
            int index = 0;
            foreach (KeyValuePair<int, string> card in EnumerateDeviceDirectories())
            {
                devices.Add(ReadDevice(card.Value, index));
                index++;
            }

            return devices;
        }

        /// <summary>
        /// Determine whether an AMD GPU is an APU that relies on GTT (host) memory.
        /// </summary>
        /// <param name="vramTotalBytes">VRAM total bytes.</param>
        /// <param name="gttTotalBytes">GTT total bytes.</param>
        /// <param name="model">Model name.</param>
        /// <param name="pciDeviceId">PCI device identifier (hex, without 0x), when known.</param>
        /// <returns>True for APUs.</returns>
        public static bool IsApu(double? vramTotalBytes, double? gttTotalBytes, string? model, string? pciDeviceId)
        {
            string name = model ?? String.Empty;
            if (name.IndexOf("MI300A", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Ryzen", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 8060S", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 8050S", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 890M", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 880M", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 780M", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 760M", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Radeon 740M", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (!String.IsNullOrWhiteSpace(pciDeviceId) && _KnownApuDeviceIds.Contains(pciDeviceId.Trim().ToLowerInvariant()))
            {
                return true;
            }

            // A small BIOS carve-out next to a much larger GTT aperture indicates an APU. The 2 GiB ceiling keeps
            // discrete cards with modest VRAM on hosts with a lot of RAM from being misclassified.
            if (vramTotalBytes.HasValue && gttTotalBytes.HasValue && vramTotalBytes.Value > 0D && gttTotalBytes.Value > 0D)
            {
                return vramTotalBytes.Value <= 2D * 1024D * 1024D * 1024D && vramTotalBytes.Value * 8D <= gttTotalBytes.Value;
            }

            return false;
        }

        private static readonly HashSet<string> _KnownApuDeviceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "1586", // Strix Halo (Ryzen AI Max)
            "150e", // Strix Point
            "15bf", // Phoenix
            "15c8", // Phoenix 2
            "1900", // Hawk Point / Krackan
            "164e", // Raphael iGPU
            "1681", // Rembrandt
            "74a0"  // MI300A
        };

        internal SortedDictionary<int, string> EnumerateDeviceDirectories()
        {
            SortedDictionary<int, string> cards = new SortedDictionary<int, string>();
            if (!Directory.Exists(_DrmRoot))
            {
                return cards;
            }

            foreach (string entry in SafeEnumerateDirectories(_DrmRoot))
            {
                string name = Path.GetFileName(entry);
                if (!_CardName.IsMatch(name))
                {
                    continue;
                }

                string deviceDirectory = Path.Combine(entry, "device");
                string vendor = ReadText(Path.Combine(deviceDirectory, "vendor"));
                if (!String.Equals(vendor, AmdPciVendorId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Int32.TryParse(name.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cardIndex))
                {
                    cards[cardIndex] = deviceDirectory;
                }
            }

            return cards;
        }

        private static AmdGpuDeviceTelemetry ReadDevice(string deviceDirectory, int index)
        {
            Dictionary<string, string> uevent = ReadUevent(Path.Combine(deviceDirectory, "uevent"));
            uevent.TryGetValue("PCI_SLOT_NAME", out string? slot);
            uevent.TryGetValue("PCI_ID", out string? pciId);
            string pciDeviceId = String.Empty;
            if (!String.IsNullOrWhiteSpace(pciId) && pciId.Contains(':', StringComparison.Ordinal))
            {
                pciDeviceId = pciId.Split(':')[1].ToLowerInvariant();
            }

            string productName = ReadText(Path.Combine(deviceDirectory, "product_name"));
            AmdGpuDeviceTelemetry device = new AmdGpuDeviceTelemetry
            {
                DeviceIndex = index,
                Source = GpuTelemetrySourceEnum.AmdSysfs,
                BusId = NormalizeBusId(slot ?? String.Empty),
                Uuid = ReadText(Path.Combine(deviceDirectory, "unique_id")),
                Model = !String.IsNullOrWhiteSpace(productName)
                    ? productName
                    : (String.IsNullOrWhiteSpace(pciId) ? "AMD GPU" : "AMD GPU [" + pciId + "]"),
                DriverVersion = ReadDriverVersion(deviceDirectory)
            };

            double? busy = ReadDouble(Path.Combine(deviceDirectory, "gpu_busy_percent"));
            if (busy.HasValue)
            {
                device.Metrics.GpuUtilizationPercent = busy.Value;
                device.FieldSources["utilization"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            double? memoryBusy = ReadDouble(Path.Combine(deviceDirectory, "mem_busy_percent"));
            if (memoryBusy.HasValue)
            {
                device.MemoryActivityPercent = memoryBusy.Value;
                device.FieldSources["memoryActivity"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            double? vramTotal = ReadDouble(Path.Combine(deviceDirectory, "mem_info_vram_total"));
            double? vramUsed = ReadDouble(Path.Combine(deviceDirectory, "mem_info_vram_used"));
            double? gttTotal = ReadDouble(Path.Combine(deviceDirectory, "mem_info_gtt_total"));
            double? gttUsed = ReadDouble(Path.Combine(deviceDirectory, "mem_info_gtt_used"));

            if (gttTotal.HasValue)
            {
                device.GttTotalMegabytes = GpuMetricMath.BytesToMegabytes(gttTotal.Value);
                device.GttUsedMegabytes = gttUsed.HasValue ? GpuMetricMath.BytesToMegabytes(gttUsed.Value) : null;
                device.FieldSources["gtt"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            device.UnifiedMemory = IsApu(vramTotal, gttTotal, device.Model, pciDeviceId);
            if (vramTotal.HasValue)
            {
                device.Metrics.MemoryTotalMegabytes = GpuMetricMath.BytesToMegabytes(vramTotal.Value);
                device.Metrics.MemoryUsedMegabytes = GpuMetricMath.BytesToMegabytes(vramUsed ?? 0D);
                device.Metrics.MemoryFreeMegabytes = Math.Max(0D, device.Metrics.MemoryTotalMegabytes - device.Metrics.MemoryUsedMegabytes);
                device.Metrics.MemorySource = "vram";
                device.Metrics.MemoryShared = false;
                device.FieldSources["memory"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            string? hwmon = FindHwmonDirectory(deviceDirectory);
            if (hwmon != null)
            {
                ApplyHwmon(hwmon, device);
            }

            return device;
        }

        private static void ApplyHwmon(string hwmon, AmdGpuDeviceTelemetry device)
        {
            Dictionary<string, double> temperatures = ReadLabeledTemperatures(hwmon);
            if (temperatures.TryGetValue("edge", out double edge))
            {
                device.Metrics.TemperatureCelsius = edge;
                device.FieldSources["temperature"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            if (temperatures.TryGetValue("junction", out double junction))
            {
                device.HotspotTemperatureCelsius = junction;
                device.FieldSources["hotspotTemperature"] = GpuTelemetrySourceEnum.AmdSysfs;
                if (!device.Metrics.TemperatureCelsius.HasValue)
                {
                    device.Metrics.TemperatureCelsius = junction;
                    device.FieldSources["temperature"] = GpuTelemetrySourceEnum.AmdSysfs;
                }
            }

            if (temperatures.TryGetValue("mem", out double memory))
            {
                device.MemoryTemperatureCelsius = memory;
                device.FieldSources["memoryTemperature"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            double? power = ReadDouble(Path.Combine(hwmon, "power1_average")) ?? ReadDouble(Path.Combine(hwmon, "power1_input"));
            if (power.HasValue)
            {
                device.Metrics.PowerUsageWatts = power.Value / 1000000D;
                device.FieldSources["power"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            Dictionary<string, double> frequencies = ReadLabeledFrequencies(hwmon);
            if (frequencies.TryGetValue("sclk", out double sclk))
            {
                device.Metrics.SmClockMHz = sclk / 1000000D;
                device.FieldSources["smClock"] = GpuTelemetrySourceEnum.AmdSysfs;
            }

            if (frequencies.TryGetValue("mclk", out double mclk))
            {
                device.Metrics.MemoryClockMHz = mclk / 1000000D;
                device.FieldSources["memoryClock"] = GpuTelemetrySourceEnum.AmdSysfs;
            }
        }

        private static Dictionary<string, double> ReadLabeledTemperatures(string hwmon)
        {
            Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= 8; i++)
            {
                double? raw = ReadDouble(Path.Combine(hwmon, "temp" + i.ToString(CultureInfo.InvariantCulture) + "_input"));
                if (!raw.HasValue)
                {
                    continue;
                }

                string label = ReadText(Path.Combine(hwmon, "temp" + i.ToString(CultureInfo.InvariantCulture) + "_label"));
                if (String.IsNullOrWhiteSpace(label))
                {
                    label = i == 1 ? "edge" : "temp" + i.ToString(CultureInfo.InvariantCulture);
                }

                double celsius = raw.Value / 1000D;
                if (celsius > 0D && celsius < 200D)
                {
                    values[label.Trim()] = celsius;
                }
            }

            return values;
        }

        private static Dictionary<string, double> ReadLabeledFrequencies(string hwmon)
        {
            Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= 4; i++)
            {
                double? raw = ReadDouble(Path.Combine(hwmon, "freq" + i.ToString(CultureInfo.InvariantCulture) + "_input"));
                if (!raw.HasValue)
                {
                    continue;
                }

                string label = ReadText(Path.Combine(hwmon, "freq" + i.ToString(CultureInfo.InvariantCulture) + "_label"));
                if (String.IsNullOrWhiteSpace(label))
                {
                    label = i == 1 ? "sclk" : (i == 2 ? "mclk" : "freq" + i.ToString(CultureInfo.InvariantCulture));
                }

                values[label.Trim()] = raw.Value;
            }

            return values;
        }

        private static string? FindHwmonDirectory(string deviceDirectory)
        {
            string root = Path.Combine(deviceDirectory, "hwmon");
            if (!Directory.Exists(root))
            {
                return null;
            }

            return SafeEnumerateDirectories(root).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
        }

        private static string ReadDriverVersion(string deviceDirectory)
        {
            string version = ReadText(Path.Combine(deviceDirectory, "driver", "module", "version"));
            if (!String.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            return Environment.OSVersion.Version.ToString();
        }

        internal static string NormalizeBusId(string busId)
        {
            return (busId ?? String.Empty).Trim().ToLowerInvariant();
        }

        internal static Dictionary<string, string> ReadUevent(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string text = ReadText(path);
            foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int separator = line.IndexOf('=');
                if (separator > 0)
                {
                    values[line.Substring(0, separator)] = line.Substring(separator + 1);
                }
            }

            return values;
        }

        internal static string ReadText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path).Trim() : String.Empty;
            }
            catch (Exception)
            {
                return String.Empty;
            }
        }

        internal static double? ReadDouble(string path)
        {
            string text = ReadText(path);
            if (String.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return Double.TryParse(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
        }

        internal static IEnumerable<string> SafeEnumerateDirectories(string path)
        {
            try
            {
                return Directory.EnumerateDirectories(path).ToList();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }
    }
}
