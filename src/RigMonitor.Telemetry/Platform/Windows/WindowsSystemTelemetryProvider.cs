namespace RigMonitor.Telemetry.Platform.Windows
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Telemetry.Platform.Shared;

    /// <summary>
    /// Windows system telemetry provider.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class WindowsSystemTelemetryProvider : ISystemTelemetryProvider
    {
        private readonly PerformanceCounter _CpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private readonly PerformanceCounter _DiskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Reads/sec", "_Total");
        private readonly PerformanceCounter _DiskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Writes/sec", "_Total");
        private readonly PerformanceCounter _DiskReadQueueCounter = new PerformanceCounter("PhysicalDisk", "Avg. Disk Read Queue Length", "_Total");
        private readonly PerformanceCounter _DiskWriteQueueCounter = new PerformanceCounter("PhysicalDisk", "Avg. Disk Write Queue Length", "_Total");

        public Task WarmupAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _CpuCounter.NextValue();
            _DiskReadCounter.NextValue();
            _DiskWriteCounter.NextValue();
            _DiskReadQueueCounter.NextValue();
            _DiskWriteQueueCounter.NextValue();
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
                LogicalCoreCount = Environment.ProcessorCount,
                UtilizationPercent = _CpuCounter.NextValue()
            };

            return Task.FromResult(telemetry);
        }

        public Task<DiskTelemetry> GetDiskTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DiskTelemetry telemetry = new DiskTelemetry
            {
                ReadOperationsPerSecond = _DiskReadCounter.NextValue(),
                WriteOperationsPerSecond = _DiskWriteCounter.NextValue(),
                ReadQueueDepth = _DiskReadQueueCounter.NextValue(),
                WriteQueueDepth = _DiskWriteQueueCounter.NextValue(),
                Volumes = DriveInventoryReader.ReadVolumes()
            };

            return Task.FromResult(telemetry);
        }
    }
}
