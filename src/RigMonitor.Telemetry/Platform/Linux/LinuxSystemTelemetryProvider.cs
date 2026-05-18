namespace RigMonitor.Telemetry.Platform.Linux
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Telemetry.Platform.Shared;

    /// <summary>
    /// Linux system telemetry provider.
    /// </summary>
    internal class LinuxSystemTelemetryProvider : ISystemTelemetryProvider
    {
        private long _PreviousCpuTotal = 0L;
        private long _PreviousCpuIdle = 0L;
        private bool _HasCpuSample = false;
        private long _PreviousDiskReads = 0L;
        private long _PreviousDiskWrites = 0L;
        private DateTime _PreviousDiskTimestamp = DateTime.UtcNow;
        private bool _HasDiskSample = false;

        public Task WarmupAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CaptureCpuSample();
            CaptureDiskSample();
            return Task.CompletedTask;
        }

        public Task<SystemTelemetry> GetSystemTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SystemTelemetry telemetry = new SystemTelemetry
            {
                Hostname = Environment.MachineName,
                UptimeMs = Environment.TickCount64,
                OsDescription = RuntimeInformation.OSDescription,
                OsArchitecture = RuntimeInformation.OSArchitecture,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture
            };

            return Task.FromResult(telemetry);
        }

        public Task<CpuTelemetry> GetCpuTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CpuTelemetry telemetry = new CpuTelemetry
            {
                LogicalCoreCount = Environment.ProcessorCount
            };

            (long total, long idle) = CaptureCpuSample();
            if (_HasCpuSample)
            {
                long totalDelta = Math.Max(0L, total - _PreviousCpuTotal);
                long idleDelta = Math.Max(0L, idle - _PreviousCpuIdle);
                if (totalDelta > 0)
                {
                    telemetry.UtilizationPercent = (double)(totalDelta - idleDelta) / (double)totalDelta * 100D;
                }
            }

            _PreviousCpuTotal = total;
            _PreviousCpuIdle = idle;
            _HasCpuSample = true;
            return Task.FromResult(telemetry);
        }

        public Task<DiskTelemetry> GetDiskTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (long reads, long writes, long queueDepth) = CaptureDiskSample();
            DateTime nowUtc = DateTime.UtcNow;

            DiskTelemetry telemetry = new DiskTelemetry
            {
                Volumes = DriveInventoryReader.ReadVolumes(),
                ReadQueueDepth = queueDepth
            };

            if (_HasDiskSample)
            {
                double elapsedSeconds = Math.Max(0.001D, (nowUtc - _PreviousDiskTimestamp).TotalSeconds);
                telemetry.ReadOperationsPerSecond = Math.Max(0L, reads - _PreviousDiskReads) / elapsedSeconds;
                telemetry.WriteOperationsPerSecond = Math.Max(0L, writes - _PreviousDiskWrites) / elapsedSeconds;
            }

            _PreviousDiskReads = reads;
            _PreviousDiskWrites = writes;
            _PreviousDiskTimestamp = nowUtc;
            _HasDiskSample = true;
            return Task.FromResult(telemetry);
        }

        private static (long Total, long Idle) CaptureCpuSample()
        {
            string line = File.ReadLines("/proc/stat").First();
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            long total = 0L;
            for (int i = 1; i < parts.Length; i++)
            {
                if (Int64.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                {
                    total += value;
                }
            }

            long idle = 0L;
            if (parts.Length > 4)
            {
                Int64.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long idleTicks);
                idle = idleTicks;
            }

            if (parts.Length > 5 && Int64.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ioWaitTicks))
            {
                idle += ioWaitTicks;
            }

            return (total, idle);
        }

        private static (long Reads, long Writes, long QueueDepth) CaptureDiskSample()
        {
            string[] lines = File.ReadAllLines("/proc/diskstats");
            long reads = 0L;
            long writes = 0L;
            long queueDepth = 0L;

            foreach (string line in lines)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 14)
                {
                    continue;
                }

                string deviceName = parts[2];
                if (!ShouldIncludeDevice(deviceName))
                {
                    continue;
                }

                reads += ParseLong(parts[3]);
                writes += ParseLong(parts[7]);
                queueDepth += ParseLong(parts[11]);
            }

            return (reads, writes, queueDepth);
        }

        private static bool ShouldIncludeDevice(string deviceName)
        {
            if (deviceName.StartsWith("loop", StringComparison.OrdinalIgnoreCase)) return false;
            if (deviceName.StartsWith("ram", StringComparison.OrdinalIgnoreCase)) return false;
            if (deviceName.StartsWith("dm-", StringComparison.OrdinalIgnoreCase)) return true;

            char last = deviceName[deviceName.Length - 1];
            if (Char.IsDigit(last) && !deviceName.StartsWith("nvme", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static long ParseLong(string value)
        {
            if (Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }

            return 0L;
        }
    }
}
