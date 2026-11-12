namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Apple;
    using RigMonitor.Telemetry.Platform.Mac.Native;
    using Test.Shared;

    /// <summary>
    /// Apple GPU telemetry: pure calculations and provider behavior against a fake native seam.
    /// Values are taken from an Apple M5 Max running macOS 26.6.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class AppleGpuTelemetryTests
    {
        /// <summary>
        /// The pmgr voltage-states table parses as Hz, including the zero (off) entry.
        /// </summary>
        [Fact]
        public void VoltageStatesShouldParseAsHertz()
        {
            byte[] bytes = Table(0U, 338000000U, 486000000U, 1620000000U);
            List<double> frequencies = AppleGpuMath.ParseVoltageStates(bytes);
            Assert.Equal(new double[] { 0D, 338000000D, 486000000D, 1620000000D }, frequencies);

            List<double> kilohertz = AppleGpuMath.ParseVoltageStates(Table(0U, 338000U, 1620000U));
            Assert.Equal(1620000000D, kilohertz.Last());
        }

        /// <summary>
        /// Residency maps active P-states onto the non-zero DVFS entries.
        /// </summary>
        [Fact]
        public void FrequencyShouldWeightActiveStates()
        {
            List<KeyValuePair<string, long>> residencies = new List<KeyValuePair<string, long>>
            {
                new KeyValuePair<string, long>("OFF", 600),
                new KeyValuePair<string, long>("P1", 300),
                new KeyValuePair<string, long>("P2", 100),
                new KeyValuePair<string, long>("P3", 0)
            };

            AppleGpuMath.ComputeFrequency(residencies, new double[] { 0D, 338e6, 486e6, 636e6 }, out double? frequency, out double? residency);

            Assert.Equal(40D, residency!.Value, 6);
            Assert.Equal((300D * 338D + 100D * 486D) / 400D, frequency!.Value, 6);
        }

        /// <summary>
        /// All-idle residency reports zero activity and no frequency.
        /// </summary>
        [Fact]
        public void FrequencyShouldBeNullWhenIdle()
        {
            List<KeyValuePair<string, long>> residencies = new List<KeyValuePair<string, long>>
            {
                new KeyValuePair<string, long>("OFF", 1000),
                new KeyValuePair<string, long>("P1", 0)
            };

            AppleGpuMath.ComputeFrequency(residencies, new double[] { 0D, 338e6 }, out double? frequency, out double? residency);
            Assert.Equal(0D, residency);
            Assert.Null(frequency);

            AppleGpuMath.ComputeFrequency(new List<KeyValuePair<string, long>>(), new double[] { 338e6 }, out frequency, out residency);
            Assert.Null(frequency);
            Assert.Null(residency);
        }

        /// <summary>
        /// Energy unit labels convert to joules.
        /// </summary>
        [Theory]
        [InlineData(1500L, "mJ", 1.5D)]
        [InlineData(2500000L, "uJ", 2.5D)]
        [InlineData(3000000000L, "nJ", 3D)]
        [InlineData(4L, "J", 4D)]
        public void EnergyShouldConvertUnits(long value, string unit, double joules)
        {
            Assert.Equal(joules, AppleGpuMath.EnergyToJoules(value, unit), 9);
        }

        /// <summary>
        /// GPU-named sensors are preferred; SoC die sensors are the fallback; others are ignored.
        /// </summary>
        [Fact]
        public void SensorSelectionShouldPreferGpuThenSocDie()
        {
            List<KeyValuePair<string, double>> m1 = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("GPU MTR Temp Sensor1", 48D),
                new KeyValuePair<string, double>("GPU MTR Temp Sensor4", 50D),
                new KeyValuePair<string, double>("PMU tdie1", 60D)
            };
            Assert.Equal(new double[] { 48D, 50D }, AppleGpuMath.SelectGpuSensors(m1, out string? m1Source));
            Assert.Equal("gpuSensors", m1Source);

            List<KeyValuePair<string, double>> m5 = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("PMU tdie1", 64.4D),
                new KeyValuePair<string, double>("PMU tdie2", 63.6D),
                new KeyValuePair<string, double>("PMU tcal", 51.8D),
                new KeyValuePair<string, double>("gas gauge battery", 35D),
                new KeyValuePair<string, double>("NAND CH0 temp", 66D)
            };
            Assert.Equal(new double[] { 64.4D, 63.6D }, AppleGpuMath.SelectGpuSensors(m5, out string? m5Source));
            Assert.Equal("socDie", m5Source);

            Assert.Empty(AppleGpuMath.SelectGpuSensors(null, out string? none));
            Assert.Null(none);
        }

        /// <summary>
        /// Accelerator classes map to vendors.
        /// </summary>
        [Theory]
        [InlineData("AGXAcceleratorG17X", "Apple M5 Max", "apple")]
        [InlineData("AMDRadeonX6000_AMDNavi10GraphicsAccelerator", "AMD Radeon Pro 5500M", "amd")]
        [InlineData("IntelAccelerator", "Intel UHD Graphics 630", "intel")]
        public void AcceleratorVendorShouldMap(string ioClass, string model, string vendor)
        {
            Assert.Equal(vendor, AppleGpuMath.VendorForAccelerator(ioClass, model));
        }

        /// <summary>
        /// The provider builds the Apple section from every native source.
        /// </summary>
        [Fact]
        public async Task ProviderShouldBuildSectionFromAllSources()
        {
            FakeAppleNative native = new FakeAppleNative();
            ManualTimeProvider time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 12, 19, 0, 0, TimeSpan.Zero));
            AppleGpuTelemetryProvider provider = new AppleGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Mac, () => native, time);

            GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
            provider.SampleOnce();
            AppleGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);
            provider.Dispose();

            Assert.True(probe.Available);
            Assert.Equal(GpuTelemetrySourceEnum.AppleNative, probe.Source);
            Assert.Equal(new[] { "ioKit", "metal", "processInfo", "ioReport", "hidSensors" }, probe.Features);

            Assert.NotNull(telemetry);
            Assert.Equal("Apple M5 Max", telemetry.Chip);
            Assert.Equal(40, telemetry.GpuCoreCount);
            Assert.Equal("apple10", telemetry.MetalFamily);
            Assert.Equal(GpuThermalStateEnum.Fair, telemetry.ThermalState);
            Assert.Equal("socDie", telemetry.TemperatureSource);
            Assert.Equal(131072D, telemetry.HostMemoryTotalMegabytes);
            Assert.Equal(110100.484375D, telemetry.RecommendedMaxWorkingSetMegabytes!.Value, 3);
            Assert.Null(telemetry.WiredLimitMegabytes);

            AppleGpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
            Assert.Equal(GpuVendorEnum.Apple, device.Vendor);
            Assert.True(device.UnifiedMemory);
            Assert.Equal(14D, device.Metrics.GpuUtilizationPercent);
            Assert.Equal(14D, device.RendererUtilizationPercent);
            Assert.Equal(11D, device.TilerUtilizationPercent);
            Assert.Equal(1766916096D / 1024 / 1024, device.Metrics.MemoryUsedMegabytes, 6);
            Assert.Equal(131072D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal("unifiedSystemMemory", device.Metrics.MemorySource);
            Assert.True(device.Metrics.MemoryShared);
            Assert.Equal(64D, device.Metrics.TemperatureCelsius!.Value, 6);
            Assert.Equal(64.4D, device.MaxTemperatureCelsius);
            Assert.Equal(0.3D, device.Metrics.PowerUsageWatts!.Value, 6);
            Assert.Equal(338D, device.Metrics.SmClockMHz!.Value, 6);
            Assert.Equal(33.333333D, device.ActiveResidencyPercent!.Value, 4);
            Assert.True(native.Disposed);
        }

        /// <summary>
        /// Kill-switch settings stop IOReport and IOHID from being used.
        /// </summary>
        [Fact]
        public async Task KillSwitchesShouldDisableOptionalSources()
        {
            FakeAppleNative native = new FakeAppleNative();
            TelemetrySettings settings = new TelemetrySettings { AppleIOReportEnabled = false, AppleHidSensorsEnabled = false };
            AppleGpuTelemetryProvider provider = new AppleGpuTelemetryProvider(settings, HostPlatformEnum.Mac, () => native);

            GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
            AppleGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

            Assert.DoesNotContain("ioReport", probe.Features);
            Assert.DoesNotContain("hidSensors", probe.Features);
            Assert.Equal(0, native.IOReportStarts);
            Assert.Equal(0, native.HidReads);
            AppleGpuDeviceTelemetry device = Assert.Single(telemetry!.Devices);
            Assert.Null(device.Metrics.PowerUsageWatts);
            Assert.Null(device.Metrics.TemperatureCelsius);
        }

        /// <summary>
        /// A missing optional framework only nulls the fields that need it.
        /// </summary>
        [Fact]
        public async Task MissingOptionalFrameworksShouldOnlyNullTheirFields()
        {
            FakeAppleNative native = new FakeAppleNative { IOReportAvailable = false, HidThrows = true, MetalAvailable = false };
            AppleGpuTelemetryProvider provider = new AppleGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Mac, () => native);

            GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
            AppleGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

            Assert.True(probe.Available);
            Assert.Equal(new[] { "ioKit", "processInfo" }, probe.Features);
            Assert.NotNull(telemetry);
            Assert.Null(telemetry.RecommendedMaxWorkingSetMegabytes);
            Assert.Null(telemetry.MetalFamily);
            AppleGpuDeviceTelemetry device = Assert.Single(telemetry.Devices);
            Assert.Equal(14D, device.Metrics.GpuUtilizationPercent);
            Assert.Null(device.Metrics.PowerUsageWatts);
        }

        /// <summary>
        /// Stale sampler readings are dropped instead of being repeated forever.
        /// </summary>
        [Fact]
        public async Task StaleSamplerReadingsShouldBeDropped()
        {
            FakeAppleNative native = new FakeAppleNative();
            ManualTimeProvider time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 12, 19, 0, 0, TimeSpan.Zero));
            AppleGpuTelemetryProvider provider = new AppleGpuTelemetryProvider(new TelemetrySettings { AppleSamplerIntervalMs = 1000 }, HostPlatformEnum.Mac, () => native, time);

            await provider.ProbeAsync(CancellationToken.None);
            provider.SampleOnce();
            time.Advance(TimeSpan.FromSeconds(10));
            AppleGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);
            provider.Dispose();

            Assert.Null(Assert.Single(telemetry!.Devices).Metrics.PowerUsageWatts);
        }

        /// <summary>
        /// Non-macOS hosts and disabled settings never load the native layer.
        /// </summary>
        [Fact]
        public async Task ProbeShouldRespectPlatformAndSettings()
        {
            int created = 0;
            Func<IAppleGpuNative> factory = () =>
            {
                created++;
                return new FakeAppleNative();
            };

            Assert.False((await new AppleGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Linux, factory).ProbeAsync(CancellationToken.None)).Available);
            Assert.False((await new AppleGpuTelemetryProvider(new TelemetrySettings { AppleGpuEnabled = false }, HostPlatformEnum.Mac, factory).ProbeAsync(CancellationToken.None)).Available);
            Assert.Equal(0, created);
        }

        /// <summary>
        /// Intel Macs report discrete GPUs with their own vendor and VRAM.
        /// </summary>
        [Fact]
        public void IntelMacDiscreteGpuShouldReportVendorAndVram()
        {
            List<AppleAcceleratorInfo> accelerators = new List<AppleAcceleratorInfo>
            {
                new AppleAcceleratorInfo
                {
                    IOClass = "AMDRadeonX6000_AMDNavi14GraphicsAccelerator",
                    Model = "AMD Radeon Pro 5500M",
                    PerformanceStatistics =
                    {
                        { "Device Utilization %", 22D },
                        { "vramUsedBytes", 2D * 1024 * 1024 * 1024 },
                        { "vramFreeBytes", 6D * 1024 * 1024 * 1024 }
                    }
                }
            };

            AppleGpuTelemetry? telemetry = AppleGpuTelemetryProvider.Build(accelerators, null, null, null, new List<double>(), null, new List<string>());

            AppleGpuDeviceTelemetry device = Assert.Single(telemetry!.Devices);
            Assert.Equal(GpuVendorEnum.Amd, device.Vendor);
            Assert.False(device.UnifiedMemory);
            Assert.Equal(8192D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal(22D, device.Metrics.GpuUtilizationPercent);
            Assert.Null(device.Metrics.TemperatureCelsius);
        }

        private static byte[] Table(params uint[] frequencies)
        {
            List<byte> bytes = new List<byte>();
            foreach (uint frequency in frequencies)
            {
                bytes.AddRange(BitConverter.GetBytes(frequency));
                bytes.AddRange(BitConverter.GetBytes(700U));
            }

            return bytes.ToArray();
        }

        internal sealed class FakeAppleNative : IAppleGpuNative
        {
            public bool IOReportAvailable { get; set; } = true;

            public bool HidThrows { get; set; } = false;

            public bool MetalAvailable { get; set; } = true;

            public int IOReportStarts { get; private set; } = 0;

            public int HidReads { get; private set; } = 0;

            public bool Disposed { get; private set; } = false;

            public List<AppleAcceleratorInfo> ReadAccelerators()
            {
                return new List<AppleAcceleratorInfo>
                {
                    new AppleAcceleratorInfo
                    {
                        RegistryName = "AGXAcceleratorG17X",
                        IOClass = "AGXAcceleratorG17X",
                        Model = "Apple M5 Max",
                        CoreCount = 40,
                        PerformanceStatistics =
                        {
                            { "Device Utilization %", 14D },
                            { "Renderer Utilization %", 14D },
                            { "Tiler Utilization %", 11D },
                            { "In use system memory", 1766916096D },
                            { "Alloc system memory", 6672891904D }
                        }
                    }
                };
            }

            public AppleGpuStaticInfo ReadStatic()
            {
                return new AppleGpuStaticInfo
                {
                    MetalDeviceName = MetalAvailable ? "Apple M5 Max" : null,
                    HasUnifiedMemory = MetalAvailable ? true : null,
                    RecommendedMaxWorkingSetBytes = MetalAvailable ? 115448725504D : null,
                    MetalFamily = MetalAvailable ? "apple10" : null,
                    HostMemoryBytes = 137438953472D,
                    GpuFrequenciesHz = new List<double> { 0D, 338e6, 486e6, 636e6 }
                };
            }

            public long? ReadWiredLimitMegabytes() => 0L;

            public int? ReadThermalState() => 1;

            public bool TryStartIOReport(out string? error)
            {
                IOReportStarts++;
                error = IOReportAvailable ? null : "libIOReport.dylib could not be loaded.";
                return IOReportAvailable;
            }

            public AppleIOReportDelta? SampleIOReport()
            {
                return new AppleIOReportDelta
                {
                    ElapsedSeconds = 1D,
                    GpuEnergyJoules = 0.3D,
                    GpuStateResidencies =
                    {
                        new KeyValuePair<string, long>("OFF", 200),
                        new KeyValuePair<string, long>("P1", 100),
                        new KeyValuePair<string, long>("P2", 0)
                    }
                };
            }

            public List<KeyValuePair<string, double>>? ReadHidTemperatures(Func<string, bool>? include = null)
            {
                HidReads++;
                if (HidThrows) throw new EntryPointNotFoundException("IOHIDEventSystemClientCreate");
                return new List<KeyValuePair<string, double>>
                {
                    new KeyValuePair<string, double>("PMU tdie1", 64.4D),
                    new KeyValuePair<string, double>("PMU tdie2", 63.6D),
                    new KeyValuePair<string, double>("gas gauge battery", 35D)
                };
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
