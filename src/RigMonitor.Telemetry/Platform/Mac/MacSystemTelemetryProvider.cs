namespace RigMonitor.Telemetry.Platform.Mac
{
    using System;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Telemetry.Platform.Shared;

    /// <summary>
    /// macOS system telemetry provider.
    /// </summary>
    internal class MacSystemTelemetryProvider : ISystemTelemetryProvider
    {
        public Task WarmupAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        public async Task<CpuTelemetry> GetCpuTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CpuTelemetry telemetry = new CpuTelemetry
            {
                LogicalCoreCount = Environment.ProcessorCount
            };

            string? output = await ProcessCommandRunner.RunAsync("/usr/bin/top", "-l 1 -n 0", cancellationToken).ConfigureAwait(false);
            if (!String.IsNullOrWhiteSpace(output))
            {
                string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string line in lines)
                {
                    if (!line.Contains("CPU usage", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int idleIndex = line.IndexOf("idle", StringComparison.OrdinalIgnoreCase);
                    if (idleIndex > 0)
                    {
                        int percentIndex = line.LastIndexOf('%', idleIndex);
                        string percentText = line.Substring(line.LastIndexOf(' ', percentIndex) + 1, percentIndex - line.LastIndexOf(' ', percentIndex) - 1);
                        if (Double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out double idlePercent))
                        {
                            telemetry.UtilizationPercent = Math.Clamp(100D - idlePercent, 0D, 100D);
                        }
                    }
                }
            }

            return telemetry;
        }

        public Task<DiskTelemetry> GetDiskTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DiskTelemetry telemetry = new DiskTelemetry
            {
                Volumes = DriveInventoryReader.ReadVolumes()
            };

            return Task.FromResult(telemetry);
        }
    }
}
