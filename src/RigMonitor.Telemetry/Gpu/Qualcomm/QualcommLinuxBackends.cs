namespace RigMonitor.Telemetry.Gpu.Qualcomm
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Telemetry.Gpu.Amd;

    /// <summary>
    /// Turns Adreno identifiers into display names.
    /// </summary>
    public static class AdrenoModelNames
    {
        private static readonly Regex _NumberedCompatible = new Regex("qcom,adreno-(?<model>[0-9]{3})(\\.[0-9]+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _ChipIdCompatible = new Regex("qcom,adreno-(?<chip>[0-9a-fA-F]{8})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _KgslModel = new Regex("^Adreno\\s*(?<model>[0-9A-Za-z\\-]+?)(v[0-9]+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Display name from a device-tree compatible list (NUL- or newline-separated).
        /// </summary>
        /// <param name="compatible">Compatible strings.</param>
        /// <returns>Display name.</returns>
        public static string FromCompatible(string? compatible)
        {
            foreach (string entry in SplitCompatible(compatible))
            {
                Match numbered = _NumberedCompatible.Match(entry);
                if (numbered.Success)
                {
                    return "Qualcomm Adreno " + numbered.Groups["model"].Value;
                }

                Match chip = _ChipIdCompatible.Match(entry);
                if (chip.Success)
                {
                    return "Qualcomm Adreno GPU (chip 0x" + chip.Groups["chip"].Value.ToLowerInvariant() + ")";
                }
            }

            return "Qualcomm Adreno GPU";
        }

        /// <summary>
        /// Display name from the kgsl gpu_model attribute (for example "Adreno740v2").
        /// </summary>
        /// <param name="kgslModel">kgsl model.</param>
        /// <returns>Display name.</returns>
        public static string FromKgsl(string? kgslModel)
        {
            string value = (kgslModel ?? String.Empty).Trim();
            Match match = _KgslModel.Match(value);
            if (match.Success)
            {
                return "Qualcomm Adreno " + match.Groups["model"].Value;
            }

            return String.IsNullOrWhiteSpace(value) ? "Qualcomm Adreno GPU" : "Qualcomm " + value;
        }

        /// <summary>
        /// First Adreno compatible entry.
        /// </summary>
        /// <param name="compatible">Compatible strings.</param>
        /// <returns>Entry or null.</returns>
        public static string? FirstAdrenoEntry(string? compatible)
        {
            return SplitCompatible(compatible).FirstOrDefault(entry => entry.StartsWith("qcom,adreno", StringComparison.OrdinalIgnoreCase));
        }

        internal static IEnumerable<string> SplitCompatible(string? compatible)
        {
            return (compatible ?? String.Empty)
                .Split(new char[] { '\0', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    /// <summary>
    /// Reads Adreno GPUs driven by the upstream Linux drm/msm driver: devfreq for frequency, DRM fdinfo for utilization and memory,
    /// and thermal zones for temperature.
    /// </summary>
    public class DrmMsmBackend
    {
        private readonly string _SysRoot;
        private readonly string _ProcRoot;
        private readonly object _Lock = new object();
        private Dictionary<string, double> _PreviousEngineNs = new Dictionary<string, double>(StringComparer.Ordinal);
        private DateTime? _PreviousSampleUtc = null;

        /// <summary>
        /// Instantiate the backend.
        /// </summary>
        /// <param name="sysRoot">sysfs root (normally /sys).</param>
        /// <param name="procRoot">procfs root (normally /proc).</param>
        public DrmMsmBackend(string sysRoot, string procRoot)
        {
            _SysRoot = String.IsNullOrWhiteSpace(sysRoot) ? "/sys" : sysRoot;
            _ProcRoot = String.IsNullOrWhiteSpace(procRoot) ? "/proc" : procRoot;
        }

        /// <summary>
        /// sysfs root.
        /// </summary>
        public string Root
        {
            get
            {
                return _SysRoot;
            }
        }

        /// <summary>
        /// Whether an Adreno devfreq device is present.
        /// </summary>
        /// <returns>True when present.</returns>
        public bool HasDevices()
        {
            return FindGpuDevfreq().Count > 0;
        }

        /// <summary>
        /// Read devices. Utilization requires two samples; the first call after construction primes the counters.
        /// </summary>
        /// <param name="nowUtc">Sample time.</param>
        /// <returns>Devices.</returns>
        public List<QualcommGpuDeviceTelemetry> ReadDevices(DateTime nowUtc)
        {
            List<QualcommGpuDeviceTelemetry> devices = new List<QualcommGpuDeviceTelemetry>();
            List<string> devfreq = FindGpuDevfreq();
            if (devfreq.Count < 1)
            {
                return devices;
            }

            FdinfoTotals totals = ReadFdinfo();
            double? utilization = null;
            lock (_Lock)
            {
                if (_PreviousSampleUtc.HasValue)
                {
                    double elapsedNs = (nowUtc - _PreviousSampleUtc.Value).TotalMilliseconds * 1000000D;
                    if (elapsedNs > 0D)
                    {
                        double busyNs = 0D;
                        foreach (KeyValuePair<string, double> client in totals.EngineNsByClient)
                        {
                            if (_PreviousEngineNs.TryGetValue(client.Key, out double previous) && client.Value >= previous)
                            {
                                busyNs += client.Value - previous;
                            }
                        }

                        utilization = Math.Clamp(busyNs / elapsedNs * 100D, 0D, 100D);
                    }
                }

                _PreviousEngineNs = totals.EngineNsByClient;
                _PreviousSampleUtc = nowUtc;
            }

            double? temperature = null;
            double? maxTemperature = null;
            List<double> zones = ReadGpuThermalZones();
            if (zones.Count > 0)
            {
                temperature = zones.Average();
                maxTemperature = zones.Max();
            }

            int index = 0;
            foreach (string directory in devfreq)
            {
                string compatible = AmdSysfsBackend.ReadText(Path.Combine(directory, "device", "of_node", "compatible"));
                QualcommGpuDeviceTelemetry device = new QualcommGpuDeviceTelemetry
                {
                    DeviceIndex = index,
                    Source = GpuTelemetrySourceEnum.DrmMsm,
                    BusId = Path.GetFileName(directory),
                    Uuid = Path.GetFileName(directory),
                    Model = AdrenoModelNames.FromCompatible(compatible),
                    Compatible = AdrenoModelNames.FirstAdrenoEntry(compatible),
                    DriverVersion = Environment.OSVersion.Version.ToString()
                };

                double? current = AmdSysfsBackend.ReadDouble(Path.Combine(directory, "cur_freq"));
                double? max = AmdSysfsBackend.ReadDouble(Path.Combine(directory, "max_freq"));
                if (current.HasValue)
                {
                    device.CurrentFrequencyMHz = current.Value / 1000000D;
                    device.Metrics.SmClockMHz = device.CurrentFrequencyMHz;
                    device.FieldSources["frequency"] = GpuTelemetrySourceEnum.DrmMsm;
                }

                if (max.HasValue)
                {
                    device.MaxFrequencyMHz = max.Value / 1000000D;
                }

                // fdinfo totals are host-wide for the msm driver; attribute them to the first GPU (hosts have one Adreno).
                if (index == 0)
                {
                    if (utilization.HasValue)
                    {
                        device.Metrics.GpuUtilizationPercent = utilization.Value;
                        device.UtilizationScope = totals.AllProcessesReadable ? GpuUtilizationScopeEnum.AllProcesses : GpuUtilizationScopeEnum.OwnProcesses;
                        device.FieldSources["utilization"] = GpuTelemetrySourceEnum.DrmMsm;
                    }

                    if (totals.ResidentMemoryBytes > 0D)
                    {
                        device.Metrics.MemoryUsedMegabytes = GpuMetricMath.BytesToMegabytes(totals.ResidentMemoryBytes);
                        device.SharedMemoryUsedMegabytes = device.Metrics.MemoryUsedMegabytes;
                        device.FieldSources["memory"] = GpuTelemetrySourceEnum.DrmMsm;
                    }
                }

                if (temperature.HasValue)
                {
                    device.Metrics.TemperatureCelsius = temperature;
                    device.MaxTemperatureCelsius = maxTemperature;
                    device.FieldSources["temperature"] = GpuTelemetrySourceEnum.DrmMsm;
                }

                devices.Add(device);
                index++;
            }

            return devices;
        }

        internal List<string> FindGpuDevfreq()
        {
            List<string> result = new List<string>();
            string root = Path.Combine(_SysRoot, "class", "devfreq");
            if (!Directory.Exists(root))
            {
                return result;
            }

            foreach (string entry in AmdSysfsBackend.SafeEnumerateDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(entry);
                string compatible = AmdSysfsBackend.ReadText(Path.Combine(entry, "device", "of_node", "compatible"));
                if (AdrenoModelNames.FirstAdrenoEntry(compatible) != null
                    || (name.EndsWith(".gpu", StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(AmdSysfsBackend.ReadText(Path.Combine(entry, "cur_freq")))))
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        internal List<double> ReadGpuThermalZones()
        {
            List<double> values = new List<double>();
            string root = Path.Combine(_SysRoot, "class", "thermal");
            if (!Directory.Exists(root))
            {
                return values;
            }

            foreach (string zone in AmdSysfsBackend.SafeEnumerateDirectories(root))
            {
                if (!Path.GetFileName(zone).StartsWith("thermal_zone", StringComparison.Ordinal))
                {
                    continue;
                }

                string type = AmdSysfsBackend.ReadText(Path.Combine(zone, "type"));
                if (!type.StartsWith("gpu", StringComparison.OrdinalIgnoreCase) && type.IndexOf("-gpu", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                double? millidegrees = AmdSysfsBackend.ReadDouble(Path.Combine(zone, "temp"));
                if (millidegrees.HasValue)
                {
                    double celsius = millidegrees.Value / 1000D;
                    if (celsius > 0D && celsius < 200D)
                    {
                        values.Add(celsius);
                    }
                }
            }

            return values;
        }

        internal sealed class FdinfoTotals
        {
            public Dictionary<string, double> EngineNsByClient { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);

            public double ResidentMemoryBytes { get; set; } = 0D;

            public bool AllProcessesReadable { get; set; } = true;
        }

        internal FdinfoTotals ReadFdinfo()
        {
            FdinfoTotals totals = new FdinfoTotals();
            if (!Directory.Exists(_ProcRoot))
            {
                return totals;
            }

            HashSet<string> seenClients = new HashSet<string>(StringComparer.Ordinal);
            foreach (string processDirectory in AmdSysfsBackend.SafeEnumerateDirectories(_ProcRoot))
            {
                string pid = Path.GetFileName(processDirectory);
                if (pid.Length == 0 || !pid.All(Char.IsDigit))
                {
                    continue;
                }

                string fdinfoDirectory = Path.Combine(processDirectory, "fdinfo");
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(fdinfoDirectory).ToList();
                }
                catch (UnauthorizedAccessException)
                {
                    totals.AllProcessesReadable = false;
                    continue;
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        totals.AllProcessesReadable = false;
                        continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    ParseFdinfo(text, seenClients, totals);
                }
            }

            return totals;
        }

        internal static void ParseFdinfo(string text, HashSet<string> seenClients, FdinfoTotals totals)
        {
            if (text.IndexOf("drm-driver", StringComparison.Ordinal) < 0)
            {
                return;
            }

            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    fields[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
                }
            }

            if (!fields.TryGetValue("drm-driver", out string? driver) || !driver.StartsWith("msm", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string clientId = fields.TryGetValue("drm-client-id", out string? id) ? id : Guid.NewGuid().ToString("N");
            if (!seenClients.Add(clientId))
            {
                return;
            }

            if (fields.TryGetValue("drm-engine-gpu", out string? engine))
            {
                double? nanoseconds = ParseLeadingNumber(engine);
                if (nanoseconds.HasValue)
                {
                    totals.EngineNsByClient[clientId] = nanoseconds.Value;
                }
            }

            string? memory = null;
            if (fields.TryGetValue("drm-resident-memory", out string? resident)) memory = resident;
            else if (fields.TryGetValue("drm-total-memory", out string? total)) memory = total;
            if (memory != null)
            {
                totals.ResidentMemoryBytes += ParseSize(memory);
            }
        }

        internal static double ParseSize(string value)
        {
            double? number = ParseLeadingNumber(value);
            if (!number.HasValue)
            {
                return 0D;
            }

            string unit = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? String.Empty;
            switch (unit.ToUpperInvariant())
            {
                case "KIB":
                    return number.Value * 1024D;
                case "MIB":
                    return number.Value * 1024D * 1024D;
                case "GIB":
                    return number.Value * 1024D * 1024D * 1024D;
                default:
                    return number.Value;
            }
        }

        private static double? ParseLeadingNumber(string value)
        {
            string first = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? String.Empty;
            return Double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : null;
        }
    }

    /// <summary>
    /// Reads Adreno GPUs driven by the downstream Qualcomm kgsl kernel driver.
    /// </summary>
    public class KgslBackend
    {
        private readonly string _SysRoot;

        /// <summary>
        /// Instantiate the backend.
        /// </summary>
        /// <param name="sysRoot">sysfs root (normally /sys).</param>
        public KgslBackend(string sysRoot)
        {
            _SysRoot = String.IsNullOrWhiteSpace(sysRoot) ? "/sys" : sysRoot;
        }

        /// <summary>
        /// kgsl device directory.
        /// </summary>
        public string DevicePath
        {
            get
            {
                return Path.Combine(_SysRoot, "class", "kgsl", "kgsl-3d0");
            }
        }

        /// <summary>
        /// Whether a kgsl GPU is present.
        /// </summary>
        /// <returns>True when present.</returns>
        public bool HasDevices()
        {
            return Directory.Exists(DevicePath);
        }

        /// <summary>
        /// Read the kgsl GPU.
        /// </summary>
        /// <returns>Devices.</returns>
        public List<QualcommGpuDeviceTelemetry> ReadDevices()
        {
            List<QualcommGpuDeviceTelemetry> devices = new List<QualcommGpuDeviceTelemetry>();
            string directory = DevicePath;
            if (!Directory.Exists(directory))
            {
                return devices;
            }

            QualcommGpuDeviceTelemetry device = new QualcommGpuDeviceTelemetry
            {
                DeviceIndex = 0,
                Source = GpuTelemetrySourceEnum.Kgsl,
                BusId = "kgsl-3d0",
                Uuid = "kgsl-3d0",
                Model = AdrenoModelNames.FromKgsl(AmdSysfsBackend.ReadText(Path.Combine(directory, "gpu_model"))),
                DriverVersion = Environment.OSVersion.Version.ToString(),
                UtilizationScope = GpuUtilizationScopeEnum.Device
            };

            double? busy = AmdSysfsBackend.ReadDouble(Path.Combine(directory, "gpu_busy_percentage"));
            if (busy.HasValue)
            {
                device.Metrics.GpuUtilizationPercent = busy.Value;
                device.FieldSources["utilization"] = GpuTelemetrySourceEnum.Kgsl;
            }

            double? clock = AmdSysfsBackend.ReadDouble(Path.Combine(directory, "gpuclk"))
                ?? AmdSysfsBackend.ReadDouble(Path.Combine(directory, "devfreq", "cur_freq"));
            if (clock.HasValue)
            {
                device.CurrentFrequencyMHz = clock.Value / 1000000D;
                device.Metrics.SmClockMHz = device.CurrentFrequencyMHz;
                device.FieldSources["frequency"] = GpuTelemetrySourceEnum.Kgsl;
            }

            double? maxClock = AmdSysfsBackend.ReadDouble(Path.Combine(directory, "max_gpuclk"));
            if (maxClock.HasValue)
            {
                device.MaxFrequencyMHz = maxClock.Value / 1000000D;
            }

            double? temperature = AmdSysfsBackend.ReadDouble(Path.Combine(directory, "temp"));
            if (temperature.HasValue)
            {
                double celsius = temperature.Value > 1000D ? temperature.Value / 1000D : temperature.Value;
                if (celsius > 0D && celsius < 200D)
                {
                    device.Metrics.TemperatureCelsius = celsius;
                    device.MaxTemperatureCelsius = celsius;
                    device.FieldSources["temperature"] = GpuTelemetrySourceEnum.Kgsl;
                }
            }

            devices.Add(device);
            return devices;
        }
    }
}
