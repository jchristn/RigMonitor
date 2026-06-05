namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Services;

    /// <summary>
    /// NVIDIA DCGM telemetry parsing tests.
    /// </summary>
    public class NvidiaDcgmGpuTelemetryProviderTests
    {
        /// <summary>
        /// Verify framebuffer memory capacity and utilization are exposed from DCGM metrics.
        /// </summary>
        [Fact]
        public async Task ShouldExposeFramebufferCapacityAndUtilization()
        {
            const string rawMetrics =
                "DCGM_FI_DEV_GPU_UTIL{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\",pci_bus_id=\"0000:01:00.0\",driver_version=\"555.42\"} 73\n" +
                "DCGM_FI_DEV_FB_USED{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\"} 12288\n" +
                "DCGM_FI_DEV_FB_FREE{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\"} 36864\n" +
                "DCGM_FI_DEV_FB_TOTAL{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\"} 49152\n";

            NvidiaDcgmGpuTelemetryProvider provider = new NvidiaDcgmGpuTelemetryProvider(
                new TelemetrySettings { DcgmExporterUrl = "http://127.0.0.1:9400/metrics" },
                new StubDcgmExporterClient(rawMetrics));

            GpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

            Assert.NotNull(telemetry);
            GpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
            Assert.Equal("NVIDIA RTX 6000 Ada", device.Model);
            Assert.Equal(12288D, device.Metrics.MemoryUsedMegabytes);
            Assert.Equal(36864D, device.Metrics.MemoryFreeMegabytes);
            Assert.Equal(49152D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(25D, device.Metrics.MemoryUtilizationPercent);
            Assert.Equal("framebuffer", device.Metrics.MemorySource);
            Assert.False(device.Metrics.MemoryShared);
        }

        /// <summary>
        /// Verify framebuffer capacity is derived when the exporter emits only used and free memory.
        /// </summary>
        [Fact]
        public async Task ShouldDeriveFramebufferCapacityWhenTotalMetricIsMissing()
        {
            const string rawMetrics =
                "DCGM_FI_DEV_FB_USED{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 4090\"} 16384\n" +
                "DCGM_FI_DEV_FB_FREE{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 4090\"} 8192\n";

            NvidiaDcgmGpuTelemetryProvider provider = new NvidiaDcgmGpuTelemetryProvider(
                new TelemetrySettings { DcgmExporterUrl = "http://127.0.0.1:9400/metrics" },
                new StubDcgmExporterClient(rawMetrics));

            GpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

            Assert.NotNull(telemetry);
            GpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
            Assert.Equal(24576D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(66.66666666666666D, device.Metrics.MemoryUtilizationPercent, 10);
        }

        private sealed class StubDcgmExporterClient : IDcgmExporterClient
        {
            private readonly string _Metrics;

            public StubDcgmExporterClient(string metrics)
            {
                _Metrics = metrics;
            }

            public Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<string?>(_Metrics);
            }
        }
    }
}
