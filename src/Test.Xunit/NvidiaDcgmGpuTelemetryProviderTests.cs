namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Nvidia;

    /// <summary>
    /// Verifies DCGM exporter parsing into the NVIDIA section.
    /// </summary>
    public class NvidiaDcgmGpuTelemetryProviderTests
    {
        /// <summary>
        /// Framebuffer capacity and utilization are exposed from DCGM metrics.
        /// </summary>
        [Fact]
        public async Task ShouldExposeFramebufferCapacityAndUtilization()
        {
            const string rawMetrics =
                "DCGM_FI_DEV_GPU_UTIL{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\",pci_bus_id=\"0000:01:00.0\",driver_version=\"555.42\"} 73\n" +
                "DCGM_FI_DEV_FB_USED{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\"} 12288\n" +
                "DCGM_FI_DEV_FB_FREE{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\"} 36864\n" +
                "DCGM_FI_DEV_FB_TOTAL{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 6000 Ada\"} 49152\n";

            NvidiaGpuTelemetry? telemetry = await CreateProvider(rawMetrics).GetTelemetryAsync(CancellationToken.None);

            Assert.NotNull(telemetry);
            Assert.Equal(GpuTelemetrySourceEnum.DcgmExporter, telemetry.Source);
            Assert.Equal("http://127.0.0.1:9400/metrics", telemetry.Endpoint);
            NvidiaGpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
            Assert.Equal(GpuVendorEnum.Nvidia, device.Vendor);
            Assert.Equal(GpuTelemetrySourceEnum.DcgmExporter, device.Source);
            Assert.Equal("NVIDIA RTX 6000 Ada", device.Model);
            Assert.Equal("0000:01:00.0", device.BusId);
            Assert.Equal("555.42", device.DriverVersion);
            Assert.Equal(73D, device.Metrics.GpuUtilizationPercent);
            Assert.Equal(12288D, device.Metrics.MemoryUsedMegabytes);
            Assert.Equal(36864D, device.Metrics.MemoryFreeMegabytes);
            Assert.Equal(49152D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(25D, device.Metrics.MemoryUtilizationPercent);
            Assert.Equal("framebuffer", device.Metrics.MemorySource);
            Assert.False(device.Metrics.MemoryShared);
            Assert.False(device.UnifiedMemory);
        }

        /// <summary>
        /// Total framebuffer is derived from used plus free when the total metric is missing.
        /// </summary>
        [Fact]
        public async Task ShouldDeriveFramebufferCapacityWhenTotalMetricIsMissing()
        {
            const string rawMetrics =
                "DCGM_FI_DEV_FB_USED{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 4090\"} 16384\n" +
                "DCGM_FI_DEV_FB_FREE{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA RTX 4090\"} 8192\n";

            NvidiaGpuTelemetry? telemetry = await CreateProvider(rawMetrics).GetTelemetryAsync(CancellationToken.None);

            Assert.NotNull(telemetry);
            NvidiaGpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
            Assert.Equal(24576D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(66.66666666666666D, device.Metrics.MemoryUtilizationPercent, 10);
        }

        /// <summary>
        /// Metrics DCGM does not emit are null, not zero.
        /// </summary>
        [Fact]
        public async Task ShouldLeaveUnreportedMetricsNull()
        {
            const string rawMetrics = "DCGM_FI_DEV_GPU_UTIL{gpu=\"0\",UUID=\"GPU-1\",modelName=\"NVIDIA L4\"} 10\n";

            NvidiaGpuTelemetry? telemetry = await CreateProvider(rawMetrics).GetTelemetryAsync(CancellationToken.None);

            NvidiaGpuDeviceTelemetry device = Assert.Single(Assert.IsType<NvidiaGpuTelemetry>(telemetry).Devices);
            Assert.Null(device.Metrics.TemperatureCelsius);
            Assert.Null(device.Metrics.PowerUsageWatts);
            Assert.Null(device.Metrics.SmClockMHz);
            Assert.Null(device.Metrics.MemoryClockMHz);
            Assert.Null(device.XidErrors);
        }

        /// <summary>
        /// Optional DCGM fields map to the NVIDIA-specific device properties.
        /// </summary>
        [Fact]
        public async Task ShouldMapOptionalNvidiaFields()
        {
            const string rawMetrics =
                "DCGM_FI_DEV_GPU_TEMP{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 61\n" +
                "DCGM_FI_DEV_MEMORY_TEMP{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 55\n" +
                "DCGM_FI_DEV_POWER_USAGE{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 410.5\n" +
                "DCGM_FI_DEV_SM_CLOCK{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 1980\n" +
                "DCGM_FI_DEV_MEM_CLOCK{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 2619\n" +
                "DCGM_FI_DEV_MEM_COPY_UTIL{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 33\n" +
                "DCGM_FI_DEV_XID_ERRORS{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 2\n" +
                "DCGM_FI_DEV_ECC_DBE_VOL_TOTAL{gpu=\"1\",UUID=\"GPU-2\",modelName=\"NVIDIA H100\"} 1\n";

            NvidiaGpuTelemetry? telemetry = await CreateProvider(rawMetrics).GetTelemetryAsync(CancellationToken.None);

            NvidiaGpuDeviceTelemetry device = Assert.Single(Assert.IsType<NvidiaGpuTelemetry>(telemetry).Devices);
            Assert.Equal(1, device.DeviceIndex);
            Assert.Equal(61D, device.Metrics.TemperatureCelsius);
            Assert.Equal(55D, device.MemoryTemperatureCelsius);
            Assert.Equal(410.5D, device.Metrics.PowerUsageWatts);
            Assert.Equal(1980D, device.Metrics.SmClockMHz);
            Assert.Equal(2619D, device.Metrics.MemoryClockMHz);
            Assert.Equal(33D, device.MemoryCopyUtilizationPercent);
            Assert.Equal(2L, device.XidErrors);
            Assert.Equal(1L, device.UncorrectableEccErrors);
        }

        /// <summary>
        /// GB10 and DGX Spark parts are flagged as unified memory.
        /// </summary>
        [Fact]
        public async Task ShouldFlagGb10AsUnifiedMemory()
        {
            const string rawMetrics = "DCGM_FI_DEV_GPU_UTIL{gpu=\"0\",UUID=\"GPU-9\",modelName=\"NVIDIA GB10\"} 5\n";

            NvidiaGpuTelemetry? telemetry = await CreateProvider(rawMetrics).GetTelemetryAsync(CancellationToken.None);

            Assert.True(Assert.Single(Assert.IsType<NvidiaGpuTelemetry>(telemetry).Devices).UnifiedMemory);
            Assert.True(NvidiaDcgmGpuTelemetryProvider.IsUnifiedMemoryModel("DGX Spark"));
            Assert.False(NvidiaDcgmGpuTelemetryProvider.IsUnifiedMemoryModel("NVIDIA RTX 4090"));
        }

        /// <summary>
        /// MIG instances on the same GPU are reported as distinct devices.
        /// </summary>
        [Fact]
        public async Task ShouldSplitMigInstances()
        {
            const string rawMetrics =
                "DCGM_FI_DEV_FB_USED{gpu=\"0\",UUID=\"GPU-A\",modelName=\"NVIDIA A100\",GPU_I_ID=\"1\",GPU_I_PROFILE=\"1g.10gb\"} 100\n" +
                "DCGM_FI_DEV_FB_USED{gpu=\"0\",UUID=\"GPU-A\",modelName=\"NVIDIA A100\",GPU_I_ID=\"2\",GPU_I_PROFILE=\"3g.40gb\"} 200\n";

            NvidiaGpuTelemetry? telemetry = await CreateProvider(rawMetrics).GetTelemetryAsync(CancellationToken.None);

            NvidiaGpuTelemetry section = Assert.IsType<NvidiaGpuTelemetry>(telemetry);
            Assert.Equal(2, section.Devices.Count);
            Assert.Contains(section.Devices, device => device.MigProfile == "1g.10gb" && device.Metrics.MemoryUsedMegabytes == 100D);
            Assert.Contains(section.Devices, device => device.MigProfile == "3g.40gb" && device.Metrics.MemoryUsedMegabytes == 200D);
        }

        /// <summary>
        /// Non-DCGM bodies and empty bodies produce no section.
        /// </summary>
        [Fact]
        public async Task ShouldReturnNullForNonDcgmOrEmptyBodies()
        {
            Assert.Null(await CreateProvider("gpu_gfx_activity{gpu_id=\"0\"} 40\n").GetTelemetryAsync(CancellationToken.None));
            Assert.Null(await CreateProvider(String()).GetTelemetryAsync(CancellationToken.None));
        }

        /// <summary>
        /// Probing reports availability, source, and endpoint, and honors NvidiaEnabled.
        /// </summary>
        [Fact]
        public async Task ProbeShouldHonorAvailabilityAndSettings()
        {
            GpuProbeResult available = await CreateProvider("DCGM_FI_DEV_GPU_UTIL{gpu=\"0\"} 1\n").ProbeAsync(CancellationToken.None);
            Assert.True(available.Available);
            Assert.Equal(GpuTelemetrySourceEnum.DcgmExporter, available.Source);
            Assert.Equal("http://127.0.0.1:9400/metrics", available.Endpoint);

            GpuProbeResult missing = await CreateProvider("other_metric 1\n").ProbeAsync(CancellationToken.None);
            Assert.False(missing.Available);

            StubDcgmExporterClient client = new StubDcgmExporterClient("DCGM_FI_DEV_GPU_UTIL{gpu=\"0\"} 1\n");
            NvidiaDcgmGpuTelemetryProvider disabled = new NvidiaDcgmGpuTelemetryProvider(new TelemetrySettings { NvidiaEnabled = false }, client);
            GpuProbeResult result = await disabled.ProbeAsync(CancellationToken.None);
            Assert.False(result.Available);
            Assert.Equal(0, client.Calls);
        }

        private static string String()
        {
            return System.String.Empty;
        }

        private static NvidiaDcgmGpuTelemetryProvider CreateProvider(string rawMetrics)
        {
            return new NvidiaDcgmGpuTelemetryProvider(
                new TelemetrySettings { DcgmExporterUrl = "http://127.0.0.1:9400/metrics" },
                new StubDcgmExporterClient(rawMetrics));
        }

        private sealed class StubDcgmExporterClient : IDcgmExporterClient
        {
            private readonly string _Metrics;

            public StubDcgmExporterClient(string metrics)
            {
                _Metrics = metrics;
            }

            public int Calls { get; private set; } = 0;

            public string Endpoint
            {
                get
                {
                    return "http://127.0.0.1:9400/metrics";
                }
            }

            public Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult<string?>(_Metrics);
            }
        }
    }
}
