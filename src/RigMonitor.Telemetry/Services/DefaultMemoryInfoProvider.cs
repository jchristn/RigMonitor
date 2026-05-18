namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Management;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Telemetry.Platform.Shared;

    /// <summary>
    /// Default cross-platform memory provider.
    /// </summary>
    public class DefaultMemoryInfoProvider : IMemoryInfoProvider
    {
        /// <summary>
        /// Capture memory telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Memory telemetry.</returns>
        public async Task<MemoryTelemetry> GetMemoryTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OperatingSystem.IsWindows())
            {
                return GetWindowsMemoryTelemetry();
            }

            if (OperatingSystem.IsLinux())
            {
                return GetLinuxMemoryTelemetry();
            }

            if (OperatingSystem.IsMacOS())
            {
                return await GetMacMemoryTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }

            GCMemoryInfo info = GC.GetGCMemoryInfo();
            return BuildMemoryTelemetry(info.TotalAvailableMemoryBytes, 0L);
        }

        [SupportedOSPlatform("windows")]
        private static MemoryTelemetry GetWindowsMemoryTelemetry()
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementObject result in results)
                {
                    long totalBytes = Convert.ToInt64(result["TotalVisibleMemorySize"], CultureInfo.InvariantCulture) * 1024L;
                    long freeBytes = Convert.ToInt64(result["FreePhysicalMemory"], CultureInfo.InvariantCulture) * 1024L;
                    return BuildMemoryTelemetry(totalBytes, freeBytes);
                }
            }

            return new MemoryTelemetry();
        }

        private static MemoryTelemetry GetLinuxMemoryTelemetry()
        {
            Dictionary<string, long> values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines("/proc/meminfo");

            foreach (string line in lines)
            {
                string[] parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                string numberText = parts[1].Replace("kB", String.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (Int64.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                {
                    values[parts[0]] = value * 1024L;
                }
            }

            long total = values.TryGetValue("MemTotal", out long memTotal) ? memTotal : 0L;
            long available = values.TryGetValue("MemAvailable", out long memAvailable) ? memAvailable : 0L;
            return BuildMemoryTelemetry(total, available);
        }

        private static async Task<MemoryTelemetry> GetMacMemoryTelemetryAsync(CancellationToken cancellationToken)
        {
            string? totalOutput = await ProcessCommandRunner.RunAsync("/usr/sbin/sysctl", "-n hw.memsize", cancellationToken).ConfigureAwait(false);
            string? vmStatOutput = await ProcessCommandRunner.RunAsync("/usr/bin/vm_stat", String.Empty, cancellationToken).ConfigureAwait(false);

            long totalBytes = 0L;
            long availableBytes = 0L;
            long pageSize = 4096L;

            if (!String.IsNullOrWhiteSpace(totalOutput))
            {
                Int64.TryParse(totalOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out totalBytes);
            }

            if (!String.IsNullOrWhiteSpace(vmStatOutput))
            {
                string[] lines = vmStatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("page size of", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = line.Split(' ');
                        foreach (string part in parts)
                        {
                            if (Int64.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedSize))
                            {
                                pageSize = parsedSize;
                                break;
                            }
                        }
                    }
                }

                long freePages = ExtractMacVmStatValue(lines, "Pages free");
                long inactivePages = ExtractMacVmStatValue(lines, "Pages inactive");
                long speculativePages = ExtractMacVmStatValue(lines, "Pages speculative");
                availableBytes = (freePages + inactivePages + speculativePages) * pageSize;
            }

            return BuildMemoryTelemetry(totalBytes, availableBytes);
        }

        private static long ExtractMacVmStatValue(string[] lines, string key)
        {
            foreach (string line in lines)
            {
                if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string valueText = line.Substring(line.IndexOf(':') + 1).Replace(".", String.Empty, StringComparison.Ordinal).Trim();
                if (Int64.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                {
                    return value;
                }
            }

            return 0L;
        }

        private static MemoryTelemetry BuildMemoryTelemetry(long totalBytes, long availableBytes)
        {
            MemoryTelemetry telemetry = new MemoryTelemetry
            {
                TotalBytes = totalBytes,
                AvailableBytes = availableBytes
            };

            telemetry.UsedBytes = Math.Max(0L, telemetry.TotalBytes - telemetry.AvailableBytes);
            if (telemetry.TotalBytes > 0)
            {
                telemetry.UtilizationPercent = (double)telemetry.UsedBytes / (double)telemetry.TotalBytes * 100D;
            }

            return telemetry;
        }
    }
}
