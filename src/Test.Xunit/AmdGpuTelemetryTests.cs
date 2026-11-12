namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Amd;
    using RigMonitor.Telemetry.Gpu.Amd.Native;
    using RigMonitor.Telemetry.Gpu.Windows;

    /// <summary>
    /// AMD GPU telemetry: AMD SMI library mapping, amdgpu sysfs, the Device Metrics Exporter, backend selection, and gap-fill.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class AmdGpuTelemetryTests
    {
        private const string Mi300xExporter =
            "# HELP gpu_gfx_activity Graphics engine activity\n" +
            "# TYPE gpu_gfx_activity gauge\n" +
            "gpu_gfx_activity{gpu_id=\"0\",gpu_uuid=\"uuid-0\",card_model=\"AMD Instinct MI300X\",pci_bus_id=\"0000:0C:00.0\",driver_version=\"6.10.5\"} 87\n" +
            "gpu_gfx_activity{gpu_id=\"1\",gpu_uuid=\"uuid-1\",card_model=\"AMD Instinct MI300X\",pci_bus_id=\"0000:22:00.0\"} 12\n" +
            "gpu_used_vram{gpu_id=\"0\"} 102400\n" +
            "gpu_total_vram{gpu_id=\"0\"} 196608\n" +
            "gpu_used_vram{gpu_id=\"1\"} 2048\n" +
            "gpu_total_vram{gpu_id=\"1\"} 196608\n" +
            "gpu_free_vram{gpu_id=\"1\"} 194560\n" +
            "gpu_junction_temperature{gpu_id=\"0\"} 71\n" +
            "gpu_memory_temperature{gpu_id=\"0\"} 58\n" +
            "gpu_edge_temperature{gpu_id=\"1\"} 44\n" +
            "gpu_package_power{gpu_id=\"0\"} 612.5\n" +
            "gpu_power_usage{gpu_id=\"1\"} 140\n" +
            "gpu_clock{gpu_id=\"0\",clock_type=\"GPU_CLOCK_TYPE_SYSTEM\"} 2100\n" +
            "gpu_clock{gpu_id=\"0\",clock_type=\"GPU_CLOCK_TYPE_MEMORY\"} 1300\n" +
            "gpu_ecc_uncorrect_total{gpu_id=\"0\"} 3\n" +
            "gpu_ecc_correct_total{gpu_id=\"0\"} 17\n";

        /// <summary>
        /// Exporter output maps to devices with VRAM, temperatures, power, clocks, and ECC.
        /// </summary>
        [Fact]
        public void ExporterParserShouldMapMi300x()
        {
            List<AmdGpuDeviceTelemetry> devices = AmdMetricsExporterParser.Parse(Mi300xExporter);

            Assert.Equal(2, devices.Count);
            AmdGpuDeviceTelemetry first = devices[0];
            Assert.Equal(0, first.DeviceIndex);
            Assert.Equal("AMD Instinct MI300X", first.Model);
            Assert.Equal("0000:0c:00.0", first.BusId);
            Assert.Equal("uuid-0", first.Uuid);
            Assert.Equal(GpuTelemetrySourceEnum.AmdMetricsExporter, first.Source);
            Assert.Equal(87D, first.Metrics.GpuUtilizationPercent);
            Assert.Equal(102400D, first.Metrics.MemoryUsedMegabytes);
            Assert.Equal(196608D, first.Metrics.MemoryTotalMegabytes);
            Assert.Equal(94208D, first.Metrics.MemoryFreeMegabytes);
            Assert.Equal(71D, first.HotspotTemperatureCelsius);
            Assert.Equal(71D, first.Metrics.TemperatureCelsius);
            Assert.Equal(58D, first.MemoryTemperatureCelsius);
            Assert.Equal(612.5D, first.Metrics.PowerUsageWatts);
            Assert.Equal(2100D, first.Metrics.SmClockMHz);
            Assert.Equal(1300D, first.Metrics.MemoryClockMHz);
            Assert.Equal(3L, first.UncorrectableEccErrors);
            Assert.Equal(17L, first.CorrectableEccErrors);
            Assert.False(first.UnifiedMemory);

            AmdGpuDeviceTelemetry second = devices[1];
            Assert.Equal(44D, second.Metrics.TemperatureCelsius);
            Assert.Null(second.HotspotTemperatureCelsius);
            Assert.Equal(140D, second.Metrics.PowerUsageWatts);
            Assert.Equal(194560D, second.Metrics.MemoryFreeMegabytes);
        }

        /// <summary>
        /// Partitioned GPUs produce one device per partition.
        /// </summary>
        [Fact]
        public void ExporterParserShouldSplitPartitions()
        {
            const string metrics =
                "gpu_gfx_activity{gpu_id=\"0\",gpu_partition_id=\"0\",gpu_compute_partition_type=\"CPX\",gpu_memory_partition_type=\"NPS4\"} 10\n" +
                "gpu_gfx_activity{gpu_id=\"0\",gpu_partition_id=\"1\",gpu_compute_partition_type=\"CPX\",gpu_memory_partition_type=\"NPS4\"} 20\n";

            List<AmdGpuDeviceTelemetry> devices = AmdMetricsExporterParser.Parse(metrics);

            Assert.Equal(2, devices.Count);
            Assert.Equal("0", devices[0].PartitionId);
            Assert.Equal("1", devices[1].PartitionId);
            Assert.All(devices, device => Assert.Equal("CPX", device.ComputePartition));
            Assert.All(devices, device => Assert.Equal("NPS4", device.MemoryPartition));
        }

        /// <summary>
        /// Bodies without AMD metrics are not treated as AMD.
        /// </summary>
        [Fact]
        public void ExporterParserShouldIgnoreNonAmdBodies()
        {
            const string dcgm = "DCGM_FI_DEV_GPU_UTIL{gpu=\"0\"} 5\n";
            Assert.False(AmdMetricsExporterParser.ContainsAmdMetrics(dcgm));
            Assert.Empty(AmdMetricsExporterParser.Parse(dcgm));
            Assert.False(AmdMetricsExporterParser.ContainsAmdMetrics(null));
            Assert.True(AmdMetricsExporterParser.ContainsAmdMetrics(Mi300xExporter));
        }

        /// <summary>
        /// The sysfs backend discovers amdgpu cards, skips connectors and other vendors, and converts units.
        /// </summary>
        [Fact]
        public void SysfsBackendShouldReadDiscreteCard()
        {
            using (TempTree tree = new TempTree())
            {
                string card = CreateAmdCard(tree, "card1", "0000:03:00.0", "1002:744C", vramTotal: 24L << 30, vramUsed: 6L << 30, gttTotal: 32L << 30, gttUsed: 1L << 30);
                tree.Write("card1/device/product_name", "AMD Radeon RX 7900 XTX");
                tree.Write("card1/device/unique_id", "abc123");
                tree.Write("card1/device/gpu_busy_percent", "42");
                tree.Write("card1/device/mem_busy_percent", "7");
                tree.Write("card1/device/hwmon/hwmon3/temp1_input", "55000");
                tree.Write("card1/device/hwmon/hwmon3/temp1_label", "edge");
                tree.Write("card1/device/hwmon/hwmon3/temp2_input", "68000");
                tree.Write("card1/device/hwmon/hwmon3/temp2_label", "junction");
                tree.Write("card1/device/hwmon/hwmon3/temp3_input", "60000");
                tree.Write("card1/device/hwmon/hwmon3/temp3_label", "mem");
                tree.Write("card1/device/hwmon/hwmon3/power1_input", "305000000");
                tree.Write("card1/device/hwmon/hwmon3/freq1_input", "2500000000");
                tree.Write("card1/device/hwmon/hwmon3/freq1_label", "sclk");
                tree.Write("card1/device/hwmon/hwmon3/freq2_input", "1250000000");
                tree.Write("card1/device/hwmon/hwmon3/freq2_label", "mclk");
                tree.Write("card1-DP-1/status", "connected");
                tree.Write("card0/device/vendor", "0x10de");

                AmdSysfsBackend backend = new AmdSysfsBackend(tree.Root);
                Assert.True(backend.HasDevices());
                AmdGpuDeviceTelemetry device = Assert.Single(backend.ReadDevices());

                Assert.Equal("AMD Radeon RX 7900 XTX", device.Model);
                Assert.Equal("0000:03:00.0", device.BusId);
                Assert.Equal("abc123", device.Uuid);
                Assert.Equal(42D, device.Metrics.GpuUtilizationPercent);
                Assert.Equal(7D, device.MemoryActivityPercent);
                Assert.Equal(24576D, device.Metrics.MemoryTotalMegabytes);
                Assert.Equal(6144D, device.Metrics.MemoryUsedMegabytes);
                Assert.Equal(32768D, device.GttTotalMegabytes);
                Assert.Equal(1024D, device.GttUsedMegabytes);
                Assert.Equal(55D, device.Metrics.TemperatureCelsius);
                Assert.Equal(68D, device.HotspotTemperatureCelsius);
                Assert.Equal(60D, device.MemoryTemperatureCelsius);
                Assert.Equal(305D, device.Metrics.PowerUsageWatts);
                Assert.Equal(2500D, device.Metrics.SmClockMHz);
                Assert.Equal(1250D, device.Metrics.MemoryClockMHz);
                Assert.False(device.UnifiedMemory);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, device.FieldSources["power"]);
                Assert.True(card.Length > 0);
            }
        }

        /// <summary>
        /// Missing hwmon labels and power1_average fall back sensibly; missing files stay null.
        /// </summary>
        [Fact]
        public void SysfsBackendShouldFallBackAndLeaveMissingFieldsNull()
        {
            using (TempTree tree = new TempTree())
            {
                CreateAmdCard(tree, "card0", "0000:05:00.0", "1002:73BF", vramTotal: 16L << 30, vramUsed: 1L << 30, gttTotal: null, gttUsed: null);
                tree.Write("card0/device/hwmon/hwmon0/temp1_input", "47000");
                tree.Write("card0/device/hwmon/hwmon0/power1_average", "150000000");
                tree.Write("card0/device/hwmon/hwmon0/power1_input", "999000000");

                AmdGpuDeviceTelemetry device = Assert.Single(new AmdSysfsBackend(tree.Root).ReadDevices());

                Assert.Equal("AMD GPU [1002:73BF]", device.Model);
                Assert.Equal(47D, device.Metrics.TemperatureCelsius);
                Assert.Equal(150D, device.Metrics.PowerUsageWatts);
                Assert.Null(device.HotspotTemperatureCelsius);
                Assert.Null(device.GttTotalMegabytes);
                Assert.Null(device.Metrics.SmClockMHz);
                Assert.False(device.FieldSources.ContainsKey("utilization"));
            }
        }

        /// <summary>
        /// APUs are detected and their common memory combines VRAM and GTT.
        /// </summary>
        [Fact]
        public void SysfsApuShouldCombineVramAndGtt()
        {
            using (TempTree tree = new TempTree())
            {
                CreateAmdCard(tree, "card0", "0000:c5:00.0", "1002:1586", vramTotal: 512L << 20, vramUsed: 256L << 20, gttTotal: 96L << 30, gttUsed: 8L << 30);

                List<AmdGpuDeviceTelemetry> devices = new AmdSysfsBackend(tree.Root).ReadDevices();
                AmdGpuTelemetryProvider.ApplyApuMemory(devices);
                AmdGpuDeviceTelemetry device = Assert.Single(devices);

                Assert.True(device.UnifiedMemory);
                Assert.Equal(512D + 98304D, device.Metrics.MemoryTotalMegabytes);
                Assert.Equal(256D + 8192D, device.Metrics.MemoryUsedMegabytes);
                Assert.Equal("vramPlusGtt", device.Metrics.MemorySource);
                Assert.True(device.Metrics.MemoryShared);
            }
        }

        /// <summary>
        /// APU detection uses names, PCI ids, and a conservative VRAM-to-GTT ratio.
        /// </summary>
        [Fact]
        public void ApuDetectionShouldBeConservative()
        {
            Assert.True(AmdSysfsBackend.IsApu(null, null, "AMD Radeon 8060S", null));
            Assert.True(AmdSysfsBackend.IsApu(null, null, "AMD Instinct MI300A", null));
            Assert.True(AmdSysfsBackend.IsApu(null, null, "AMD GPU", "1586"));
            Assert.True(AmdSysfsBackend.IsApu(512D * 1024 * 1024, 64D * 1024 * 1024 * 1024, "AMD GPU", null));
            Assert.False(AmdSysfsBackend.IsApu(4D * 1024 * 1024 * 1024, 64D * 1024 * 1024 * 1024, "AMD Radeon RX 6500 XT", null));
            Assert.False(AmdSysfsBackend.IsApu(24D * 1024 * 1024 * 1024, 32D * 1024 * 1024 * 1024, "AMD Radeon RX 7900 XTX", null));
        }

        /// <summary>
        /// Library samples map to devices, marking each field's source and leaving unsupported values null.
        /// </summary>
        [Fact]
        public void LibrarySamplesShouldMapAndMarkSources()
        {
            List<AmdGpuDeviceTelemetry> devices = AmdGpuTelemetryProvider.MapLibrarySamples(new[] { Mi300Sample(0, "0000:0C:00.0") });
            AmdGpuDeviceTelemetry device = Assert.Single(devices);

            Assert.Equal("0000:0c:00.0", device.BusId);
            Assert.Equal("AMD Instinct MI300X", device.Model);
            Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, device.Source);
            Assert.Equal(91D, device.Metrics.GpuUtilizationPercent);
            Assert.Equal(196608D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(65536D, device.Metrics.MemoryUsedMegabytes);
            Assert.Equal(75D, device.Metrics.TemperatureCelsius);
            Assert.Equal(75D, device.HotspotTemperatureCelsius);
            Assert.Equal(2100D, device.Metrics.SmClockMHz);
            Assert.Equal("SPX", device.ComputePartition);
            Assert.Equal(1L, device.UncorrectableEccErrors);
            Assert.Null(device.Metrics.PowerUsageWatts);
            Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, device.FieldSources["utilization"]);
            Assert.False(device.FieldSources.ContainsKey("power"));
        }

        /// <summary>
        /// Sysfs fills only fields the library lacks, matched by PCI address, and never duplicates devices.
        /// </summary>
        [Fact]
        public void GapFillShouldFillMissingFieldsByBusId()
        {
            List<AmdGpuDeviceTelemetry> primary = AmdGpuTelemetryProvider.MapLibrarySamples(new[] { Mi300Sample(0, "0000:0c:00.0") });
            AmdGpuDeviceTelemetry sysfs = new AmdGpuDeviceTelemetry { BusId = "0000:0c:00.0", Model = "ignored" };
            sysfs.Metrics.PowerUsageWatts = 600D;
            sysfs.FieldSources["power"] = GpuTelemetrySourceEnum.AmdSysfs;
            sysfs.Metrics.GpuUtilizationPercent = 5D;
            sysfs.FieldSources["utilization"] = GpuTelemetrySourceEnum.AmdSysfs;
            AmdGpuDeviceTelemetry other = new AmdGpuDeviceTelemetry { BusId = "0000:99:00.0" };
            other.Metrics.PowerUsageWatts = 1D;
            other.FieldSources["power"] = GpuTelemetrySourceEnum.AmdSysfs;

            bool filled = AmdGpuTelemetryProvider.GapFill(primary, new List<AmdGpuDeviceTelemetry> { sysfs, other });

            Assert.True(filled);
            AmdGpuDeviceTelemetry device = Assert.Single(primary);
            Assert.Equal(600D, device.Metrics.PowerUsageWatts);
            Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, device.FieldSources["power"]);
            Assert.Equal(91D, device.Metrics.GpuUtilizationPercent);
            Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, device.FieldSources["utilization"]);
            Assert.Equal("AMD Instinct MI300X", device.Model);
        }

        /// <summary>
        /// Auto mode on Linux prefers the native library and enables sysfs gap-fill when cards exist.
        /// </summary>
        [Fact]
        public async Task AutoShouldPreferNativeLibraryWithGapFill()
        {
            using (TempTree tree = new TempTree())
            {
                CreateAmdCard(tree, "card0", "0000:0c:00.0", "1002:74A1", vramTotal: 192L << 30, vramUsed: 64L << 30, gttTotal: null, gttUsed: null);
                tree.Write("card0/device/hwmon/hwmon1/power1_average", "650000000");
                FakeAmdSmiNative native = new FakeAmdSmiNative(Mi300Sample(0, "0000:0c:00.0"));
                StubPrometheusClient exporter = new StubPrometheusClient(Mi300xExporter);
                AmdGpuTelemetryProvider provider = CreateLinuxProvider(tree.Root, "auto", native, exporter);

                GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
                AmdGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

                Assert.True(probe.Available);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, probe.Source);
                Assert.Contains("gap-fill", probe.Detail);
                Assert.Equal(0, exporter.Calls);
                Assert.NotNull(telemetry);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, telemetry.Source);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, telemetry.GapFillSource);
                Assert.Equal("6.4.0", telemetry.LibraryVersion);
                AmdGpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
                Assert.Equal(650D, device.Metrics.PowerUsageWatts);
            }
        }

        /// <summary>
        /// Auto mode falls back to sysfs when the library is missing, and to the exporter when there are no cards.
        /// </summary>
        [Fact]
        public async Task AutoShouldFallBackToSysfsThenExporter()
        {
            using (TempTree tree = new TempTree())
            {
                CreateAmdCard(tree, "card0", "0000:03:00.0", "1002:744C", vramTotal: 24L << 30, vramUsed: 1L << 30, gttTotal: null, gttUsed: null);
                AmdGpuTelemetryProvider sysfsProvider = CreateLinuxProvider(tree.Root, "auto", new FakeAmdSmiNative(initializeFails: true), new StubPrometheusClient(Mi300xExporter));

                GpuProbeResult sysfsProbe = await sysfsProvider.ProbeAsync(CancellationToken.None);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, sysfsProbe.Source);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, (await sysfsProvider.GetTelemetryAsync(CancellationToken.None))!.Source);
            }

            using (TempTree empty = new TempTree())
            {
                AmdGpuTelemetryProvider exporterProvider = CreateLinuxProvider(empty.Root, "auto", new FakeAmdSmiNative(initializeFails: true), new StubPrometheusClient(Mi300xExporter));
                GpuProbeResult exporterProbe = await exporterProvider.ProbeAsync(CancellationToken.None);
                Assert.Equal(GpuTelemetrySourceEnum.AmdMetricsExporter, exporterProbe.Source);
                AmdGpuTelemetry? telemetry = await exporterProvider.GetTelemetryAsync(CancellationToken.None);
                Assert.Equal(2, telemetry!.Devices.Count);

                AmdGpuTelemetryProvider nothing = CreateLinuxProvider(empty.Root, "auto", new FakeAmdSmiNative(initializeFails: true), new StubPrometheusClient(null));
                GpuProbeResult none = await nothing.ProbeAsync(CancellationToken.None);
                Assert.False(none.Available);
                Assert.Contains("libamd_smi", none.Detail);
                Assert.Null(await nothing.GetTelemetryAsync(CancellationToken.None));
            }
        }

        /// <summary>
        /// Forced sources never try other backends.
        /// </summary>
        [Fact]
        public async Task ForcedSourcesShouldNotTryOtherBackends()
        {
            using (TempTree tree = new TempTree())
            {
                CreateAmdCard(tree, "card0", "0000:03:00.0", "1002:744C", vramTotal: 24L << 30, vramUsed: 1L << 30, gttTotal: null, gttUsed: null);
                FakeAmdSmiNative native = new FakeAmdSmiNative(Mi300Sample(0, "0000:03:00.0"));
                StubPrometheusClient exporter = new StubPrometheusClient(Mi300xExporter);

                GpuProbeResult sysfs = await CreateLinuxProvider(tree.Root, "sysfs", native, exporter).ProbeAsync(CancellationToken.None);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, sysfs.Source);
                Assert.Equal(0, native.InitializeCalls);
                Assert.Equal(0, exporter.Calls);

                GpuProbeResult forcedExporter = await CreateLinuxProvider(tree.Root, "exporter", native, exporter).ProbeAsync(CancellationToken.None);
                Assert.Equal(GpuTelemetrySourceEnum.AmdMetricsExporter, forcedExporter.Source);
                Assert.Equal(0, native.InitializeCalls);

                FakeAmdSmiNative failing = new FakeAmdSmiNative(initializeFails: true);
                GpuProbeResult forcedNative = await CreateLinuxProvider(tree.Root, "native", failing, exporter).ProbeAsync(CancellationToken.None);
                Assert.False(forcedNative.Available);
            }
        }

        /// <summary>
        /// A native sampling failure falls back to sysfs for that sample.
        /// </summary>
        [Fact]
        public async Task NativeSampleFailureShouldFallBackToSysfs()
        {
            using (TempTree tree = new TempTree())
            {
                CreateAmdCard(tree, "card0", "0000:0c:00.0", "1002:74A1", vramTotal: 192L << 30, vramUsed: 1L << 30, gttTotal: null, gttUsed: null);
                FakeAmdSmiNative native = new FakeAmdSmiNative(Mi300Sample(0, "0000:0c:00.0"));
                AmdGpuTelemetryProvider provider = CreateLinuxProvider(tree.Root, "auto", native, new StubPrometheusClient(null));
                await provider.ProbeAsync(CancellationToken.None);

                native.ThrowOnSample = true;
                AmdGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

                Assert.NotNull(telemetry);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSysfs, telemetry.Source);
            }
        }

        /// <summary>
        /// Disabled settings and unsupported platforms report unavailable without touching backends.
        /// </summary>
        [Fact]
        public async Task ProbeShouldRespectSettingsAndPlatform()
        {
            FakeAmdSmiNative native = new FakeAmdSmiNative(Mi300Sample(0, "0000:0c:00.0"));
            AmdGpuTelemetryProvider disabled = new AmdGpuTelemetryProvider(new TelemetrySettings { AmdEnabled = false }, HostPlatformEnum.Linux, native);
            Assert.False((await disabled.ProbeAsync(CancellationToken.None)).Available);

            AmdGpuTelemetryProvider mac = new AmdGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Mac, native);
            Assert.False((await mac.ProbeAsync(CancellationToken.None)).Available);
            Assert.Equal(0, native.InitializeCalls);
        }

        /// <summary>
        /// On Windows, AMD adapters from the graphics kernel backend feed the AMD section.
        /// </summary>
        [Fact]
        public async Task WindowsShouldUseGraphicsKernelAdapters()
        {
            FakeWindowsSource windows = new FakeWindowsSource(
                new WindowsGpuAdapterReading
                {
                    VendorId = PciVendorIds.Amd,
                    DeviceId = 0x744C,
                    Description = "AMD Radeon RX 7900 XTX",
                    Luid = "0x00000000_0x0000ABCD",
                    DedicatedVideoMemoryBytes = 24D * 1024 * 1024 * 1024,
                    SharedSystemMemoryBytes = 32D * 1024 * 1024 * 1024,
                    LocalUsageBytes = 4D * 1024 * 1024 * 1024,
                    EngineUtilization = { { "3d", 30D }, { "compute", 55D } },
                    TemperatureCelsius = 61D
                },
                new WindowsGpuAdapterReading { VendorId = PciVendorIds.Nvidia, Description = "NVIDIA RTX" });

            AmdGpuTelemetryProvider provider = new AmdGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Windows, null, null, null, windows);
            GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
            AmdGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

            Assert.Equal(GpuTelemetrySourceEnum.WindowsGraphicsKernel, probe.Source);
            AmdGpuDeviceTelemetry device = Assert.Single(telemetry!.Devices);
            Assert.Equal("AMD Radeon RX 7900 XTX", device.Model);
            Assert.Equal(55D, device.Metrics.GpuUtilizationPercent);
            Assert.Equal(24576D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(4096D, device.Metrics.MemoryUsedMegabytes);
            Assert.Equal(61D, device.Metrics.TemperatureCelsius);
            Assert.False(device.UnifiedMemory);

            AmdGpuTelemetryProvider generic = new AmdGpuTelemetryProvider(new TelemetrySettings { WindowsGenericGpuEnabled = false }, HostPlatformEnum.Windows, null, null, null, windows);
            Assert.False((await generic.ProbeAsync(CancellationToken.None)).Available);
        }

        /// <summary>
        /// Native helpers format PCI addresses and normalize values defensively.
        /// </summary>
        [Fact]
        public void NativeHelpersShouldNormalizeValues()
        {
            ulong bdf = (0x0000UL << 16) | (0x0CUL << 8) | (0x00UL << 3) | 0x0UL;
            Assert.Equal("0000:0c:00.0", AmdSmiValues.FormatBdf(bdf));
            Assert.Equal("0001:22:01.1", AmdSmiValues.FormatBdf((0x1UL << 16) | (0x22UL << 8) | (0x1UL << 3) | 0x1UL));
            Assert.Equal(65D, AmdSmiValues.NormalizeTemperature(65));
            Assert.Equal(65D, AmdSmiValues.NormalizeTemperature(65000));
            Assert.Null(AmdSmiValues.NormalizeTemperature(0));
            Assert.Null(AmdSmiValues.NormalizeClock(0));
            Assert.Null(AmdSmiValues.NormalizePercent(255));
            Assert.Equal(40D, AmdSmiValues.NormalizePercent(40));
        }

        private static AmdSmiDeviceSample Mi300Sample(int index, string busId)
        {
            return new AmdSmiDeviceSample
            {
                Index = index,
                BusId = busId,
                Uuid = "uuid-" + index,
                MarketName = "AMD Instinct MI300X",
                ComputePartition = "SPX",
                MemoryPartition = "NPS1",
                GfxActivityPercent = 91D,
                UmcActivityPercent = 40D,
                VramTotalBytes = 192D * 1024 * 1024 * 1024,
                VramUsedBytes = 64D * 1024 * 1024 * 1024,
                HotspotTemperatureCelsius = 75D,
                MemoryTemperatureCelsius = 60D,
                GfxClockMHz = 2100D,
                MemoryClockMHz = 1300D,
                CorrectableEccErrors = 4L,
                UncorrectableEccErrors = 1L
            };
        }

        private static AmdGpuTelemetryProvider CreateLinuxProvider(string drmRoot, string source, IAmdSmiNative native, IPrometheusTextClient exporter)
        {
            TelemetrySettings settings = new TelemetrySettings { AmdSource = source, AmdSysfsRoot = drmRoot };
            return new AmdGpuTelemetryProvider(settings, HostPlatformEnum.Linux, native, new AmdSysfsBackend(drmRoot), exporter);
        }

        private static string CreateAmdCard(TempTree tree, string card, string slot, string pciId, long vramTotal, long vramUsed, long? gttTotal, long? gttUsed)
        {
            tree.Write(card + "/device/vendor", "0x1002");
            tree.Write(card + "/device/uevent", "DRIVER=amdgpu\nPCI_ID=" + pciId + "\nPCI_SLOT_NAME=" + slot + "\n");
            tree.Write(card + "/device/mem_info_vram_total", vramTotal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            tree.Write(card + "/device/mem_info_vram_used", vramUsed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (gttTotal.HasValue) tree.Write(card + "/device/mem_info_gtt_total", gttTotal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (gttUsed.HasValue) tree.Write(card + "/device/mem_info_gtt_used", gttUsed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Path.Combine(tree.Root, card);
        }

        internal sealed class FakeAmdSmiNative : IAmdSmiNative
        {
            private readonly List<AmdSmiDeviceSample> _Samples;
            private readonly bool _InitializeFails;

            public FakeAmdSmiNative(params AmdSmiDeviceSample[] samples)
            {
                _Samples = samples.ToList();
            }

            public FakeAmdSmiNative(bool initializeFails)
            {
                _Samples = new List<AmdSmiDeviceSample>();
                _InitializeFails = initializeFails;
            }

            public int InitializeCalls { get; private set; } = 0;

            public bool ThrowOnSample { get; set; } = false;

            public string? LibraryPath => "/opt/rocm/lib/libamd_smi.so";

            public string? LibraryVersion => "6.4.0";

            public bool TryInitialize(out string? error)
            {
                InitializeCalls++;
                error = _InitializeFails ? "libamd_smi.so was not found." : null;
                return !_InitializeFails;
            }

            public IReadOnlyList<AmdSmiDeviceSample> Sample()
            {
                if (ThrowOnSample) throw new InvalidOperationException("amdsmi failure");
                return _Samples;
            }

            public void Dispose()
            {
            }
        }

        internal sealed class StubPrometheusClient : IPrometheusTextClient
        {
            private readonly string? _Body;

            public StubPrometheusClient(string? body)
            {
                _Body = body;
            }

            public int Calls { get; private set; } = 0;

            public string Endpoint => "http://localhost:5000/metrics";

            public Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(_Body);
            }
        }

        internal sealed class FakeWindowsSource : IWindowsGpuAdapterSource
        {
            private readonly List<WindowsGpuAdapterReading> _Adapters;

            public FakeWindowsSource(params WindowsGpuAdapterReading[] adapters)
            {
                _Adapters = adapters.ToList();
            }

            public bool IsAvailable() => _Adapters.Count > 0;

            public IReadOnlyList<WindowsGpuAdapterReading> GetAdapters() => _Adapters;
        }
    }

    /// <summary>
    /// Temporary directory tree for sysfs and procfs fixtures.
    /// </summary>
    internal sealed class TempTree : IDisposable
    {
        public TempTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "RigMonitorTests", "tree-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Write(string relativePath, string content)
        {
            string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content + "\n");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (Exception)
            {
            }
        }
    }
}
