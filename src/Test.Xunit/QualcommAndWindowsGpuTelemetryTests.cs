namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Qualcomm;
    using RigMonitor.Telemetry.Gpu.Windows;
    using RigMonitor.Telemetry.Platform.Windows.Native;

    /// <summary>
    /// Qualcomm Adreno telemetry (Windows on Snapdragon, drm/msm, kgsl) and the vendor-neutral Windows graphics kernel backend.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class QualcommAndWindowsGpuTelemetryTests
    {
        /// <summary>
        /// PDH engine instances aggregate per LUID: summed per engine across processes, then max per engine type.
        /// </summary>
        [Fact]
        public void PdhEnginesShouldAggregateLikeTaskManager()
        {
            List<KeyValuePair<string, double>> instances = new List<KeyValuePair<string, double>>
            {
                Pair("pid_100_luid_0x00000000_0x0000d1b6_phys_0_eng_0_engtype_3D", 20D),
                Pair("pid_200_luid_0x00000000_0x0000d1b6_phys_0_eng_0_engtype_3D", 15D),
                Pair("pid_100_luid_0x00000000_0x0000d1b6_phys_0_eng_1_engtype_3D", 50D),
                Pair("pid_300_luid_0x00000000_0x0000d1b6_phys_0_eng_2_engtype_Compute_0", 70D),
                Pair("pid_300_luid_0x00000000_0x0000d1b6_phys_0_eng_3_engtype_VideoDecode", 5D),
                Pair("pid_300_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_Copy", 99D),
                Pair("garbage", 10D)
            };

            Dictionary<string, Dictionary<string, double>> result = WindowsPdhParser.AggregateEngines(instances);

            Dictionary<string, double> adreno = result["0x00000000_0x0000D1B6"];
            Assert.Equal(50D, adreno["3d"]);
            Assert.Equal(70D, adreno["compute"]);
            Assert.Equal(5D, adreno["videoDecode"]);
            Assert.Equal(99D, result["0x00000000_0x0000AAAA"]["copy"]);
        }

        /// <summary>
        /// Adapter memory instances sum per LUID and ignore engine instances.
        /// </summary>
        [Fact]
        public void PdhAdapterMemoryShouldSumPerLuid()
        {
            Dictionary<string, double> result = WindowsPdhParser.AggregateAdapterMemory(new[]
            {
                Pair("luid_0x00000000_0x0000d1b6_phys_0", 1000D),
                Pair("luid_0x00000000_0x0000d1b6_phys_1", 500D),
                Pair("pid_1_luid_0x00000000_0x0000d1b6_phys_0_eng_0_engtype_3D", 99D)
            });

            Assert.Equal(1500D, result["0x00000000_0x0000D1B6"]);
        }

        /// <summary>
        /// Engine type names normalize to camelCase keys.
        /// </summary>
        [Theory]
        [InlineData("3D", "3d")]
        [InlineData("Compute_1", "compute")]
        [InlineData("VideoEncode", "videoEncode")]
        [InlineData("Copy", "copy")]
        [InlineData("", "other")]
        public void EngineTypesShouldNormalize(string input, string expected)
        {
            Assert.Equal(expected, WindowsPdhParser.NormalizeEngineType(input));
        }

        /// <summary>
        /// LUIDs format the same way PDH instance names do.
        /// </summary>
        [Fact]
        public void LuidShouldMatchPdhFormat()
        {
            Assert.Equal("0x00000000_0x0000D1B6", WindowsAdapterMapper.FormatLuid(0, 0xD1B6U));
        }

        /// <summary>
        /// Windows interop structures have the native 64-bit layout.
        /// </summary>
        [Fact]
#pragma warning disable CA1416 // Layout checks read struct metadata only; no Windows API is called.
        public void WindowsInteropStructsShouldMatchNativeLayout()
        {
            Assert.True(Environment.Is64BitProcess);
            Assert.Equal(312, Marshal.SizeOf<WindowsGpuNative.DxgiAdapterDesc1>());
            Assert.Equal(256, (int)Marshal.OffsetOf<WindowsGpuNative.DxgiAdapterDesc1>(nameof(WindowsGpuNative.DxgiAdapterDesc1.VendorId)));
            Assert.Equal(272, (int)Marshal.OffsetOf<WindowsGpuNative.DxgiAdapterDesc1>(nameof(WindowsGpuNative.DxgiAdapterDesc1.DedicatedVideoMemory)));
            Assert.Equal(296, (int)Marshal.OffsetOf<WindowsGpuNative.DxgiAdapterDesc1>(nameof(WindowsGpuNative.DxgiAdapterDesc1.LuidLowPart)));
            Assert.Equal(32, Marshal.SizeOf<WindowsGpuNative.DxgiQueryVideoMemoryInfo>());
            Assert.Equal(24, Marshal.SizeOf<WindowsGpuNative.D3dkmtQueryAdapterInfo>());
            Assert.Equal(24, Marshal.SizeOf<WindowsGpuNative.PdhFmtCounterValueItem>());
            Assert.Equal(16, (int)Marshal.OffsetOf<WindowsGpuNative.PdhFmtCounterValueItem>(nameof(WindowsGpuNative.PdhFmtCounterValueItem.DoubleValue)));
            Assert.Equal(12, Marshal.SizeOf<WindowsGpuNative.D3dkmtOpenAdapterFromLuid>());
        }
#pragma warning restore CA1416

        /// <summary>
        /// The Windows backend merges sampler values into DXGI readings by LUID.
        /// </summary>
        [Fact]
        public void WindowsBackendShouldMergeSamplerValues()
        {
            FakeWindowsNative native = new FakeWindowsNative();
            using (WindowsGraphicsKernelBackend backend = new WindowsGraphicsKernelBackend(native, 10000))
            {
                Assert.True(backend.IsAvailable());
                backend.SampleOnce();
                WindowsGpuAdapterReading adapter = backend.GetAdapters().Single();

                Assert.Equal(64D, adapter.EngineUtilization["3d"]);
                Assert.Equal(2D * 1024 * 1024 * 1024, adapter.NonLocalUsageBytes);
                Assert.Equal(128D * 1024 * 1024, adapter.LocalUsageBytes);
                Assert.True(native.CountersOpened);
            }

            Assert.True(native.Disposed);
        }

        /// <summary>
        /// Windows on Snapdragon resolves to the graphics kernel backend and maps Adreno adapters.
        /// </summary>
        [Fact]
        public async Task SnapdragonWindowsShouldMapAdrenoAdapter()
        {
            AmdGpuTelemetryTests.FakeWindowsSource source = new AmdGpuTelemetryTests.FakeWindowsSource(AdrenoReading(), new WindowsGpuAdapterReading { VendorId = PciVendorIds.Intel, Description = "Intel Arc" });
            QualcommGpuTelemetryProvider provider = new QualcommGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Windows, source);

            GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
            QualcommGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

            Assert.True(probe.Available);
            Assert.Equal(GpuTelemetrySourceEnum.WindowsGraphicsKernel, probe.Source);
            QualcommGpuDeviceTelemetry device = Assert.Single(telemetry!.Devices);
            Assert.Equal(GpuVendorEnum.Qualcomm, device.Vendor);
            Assert.Equal("Snapdragon(R) X Elite - X1E78100 - Qualcomm(R) Adreno(TM) GPU", device.Model);
            Assert.True(device.UnifiedMemory);
            Assert.Equal(GpuUtilizationScopeEnum.Device, device.UtilizationScope);
            Assert.Equal(41D, device.Metrics.GpuUtilizationPercent);
            Assert.Equal(2048D, device.SharedMemoryUsedMegabytes);
            Assert.Equal(16384D, device.SharedMemoryBudgetMegabytes);
            Assert.Equal(2048D + 128D, device.Metrics.MemoryUsedMegabytes);
            Assert.Equal(16384D + 512D, device.Metrics.MemoryTotalMegabytes);
            Assert.Equal("sharedSystemMemory", device.Metrics.MemorySource);
            Assert.Equal(1250D, device.CurrentFrequencyMHz);
            Assert.Equal(1500D, device.MaxFrequencyMHz);
            Assert.Equal(1250D, device.Metrics.SmClockMHz);
            Assert.Equal(52.5D, device.Metrics.TemperatureCelsius);
            Assert.Equal("0x0c36", device.DxgiDeviceId);
            Assert.Equal(GpuTelemetrySourceEnum.WindowsGraphicsKernel, device.FieldSources["utilization"]);
        }

        /// <summary>
        /// The drm/msm backend reads devfreq, thermal zones, and fdinfo, and reports the utilization scope.
        /// </summary>
        [Fact]
        public void DrmMsmShouldReadDevfreqThermalAndFdinfo()
        {
            using (TempTree tree = new TempTree())
            {
                tree.Write("sys/class/devfreq/3d00000.gpu/cur_freq", "680000000");
                tree.Write("sys/class/devfreq/3d00000.gpu/max_freq", "1250000000");
                tree.Write("sys/class/devfreq/3d00000.gpu/device/of_node/compatible", "qcom,adreno-43050c01\0qcom,adreno\0");
                tree.Write("sys/class/devfreq/cpufreq/cur_freq", "100");
                tree.Write("sys/class/thermal/thermal_zone3/type", "gpuss-0-thermal");
                tree.Write("sys/class/thermal/thermal_zone3/temp", "51000");
                tree.Write("sys/class/thermal/thermal_zone4/type", "gpuss-1-thermal");
                tree.Write("sys/class/thermal/thermal_zone4/temp", "55000");
                tree.Write("sys/class/thermal/thermal_zone0/type", "cpu-0-0-thermal");
                tree.Write("sys/class/thermal/thermal_zone0/temp", "80000");
                WriteFdinfo(tree, "1200", "5", 7, 1000000000L, "2048 KiB");
                WriteFdinfo(tree, "1300", "4", 9, 0L, "1024 KiB");
                tree.Write("proc/self/stat", "x");

                DrmMsmBackend backend = new DrmMsmBackend(tree.Root + "/sys", tree.Root + "/proc");
                Assert.True(backend.HasDevices());
                DateTime start = new DateTime(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);
                List<QualcommGpuDeviceTelemetry> first = backend.ReadDevices(start);
                QualcommGpuDeviceTelemetry primed = Assert.Single(first);
                Assert.False(primed.FieldSources.ContainsKey("utilization"));

                WriteFdinfo(tree, "1200", "5", 7, 1000000000L + 250000000L, "2048 KiB");
                WriteFdinfo(tree, "1300", "4", 9, 250000000L, "1024 KiB");
                QualcommGpuDeviceTelemetry device = Assert.Single(backend.ReadDevices(start.AddSeconds(1)));

                Assert.Equal("Qualcomm Adreno GPU (chip 0x43050c01)", device.Model);
                Assert.Equal("qcom,adreno-43050c01", device.Compatible);
                Assert.Equal(GpuTelemetrySourceEnum.DrmMsm, device.Source);
                Assert.Equal(680D, device.CurrentFrequencyMHz);
                Assert.Equal(1250D, device.MaxFrequencyMHz);
                Assert.Equal(53D, device.Metrics.TemperatureCelsius);
                Assert.Equal(55D, device.MaxTemperatureCelsius);
                Assert.Equal(50D, device.Metrics.GpuUtilizationPercent, 6);
                Assert.Equal(GpuUtilizationScopeEnum.AllProcesses, device.UtilizationScope);
                Assert.Equal(3D, device.Metrics.MemoryUsedMegabytes, 6);
                Assert.True(device.UnifiedMemory);
            }
        }

        /// <summary>
        /// fdinfo parsing ignores non-msm drivers and de-duplicates clients shared across file descriptors.
        /// </summary>
        [Fact]
        public void FdinfoShouldIgnoreOtherDriversAndDuplicateClients()
        {
            DrmMsmBackend.FdinfoTotals totals = new DrmMsmBackend.FdinfoTotals();
            HashSet<string> seen = new HashSet<string>();
            DrmMsmBackend.ParseFdinfo("drm-driver:\tamdgpu\ndrm-client-id:\t1\ndrm-engine-gfx:\t100 ns\n", seen, totals);
            DrmMsmBackend.ParseFdinfo("drm-driver:\tmsm\ndrm-client-id:\t2\ndrm-engine-gpu:\t500 ns\ndrm-total-memory:\t1 MiB\n", seen, totals);
            DrmMsmBackend.ParseFdinfo("drm-driver:\tmsm\ndrm-client-id:\t2\ndrm-engine-gpu:\t500 ns\ndrm-total-memory:\t1 MiB\n", seen, totals);
            DrmMsmBackend.ParseFdinfo("pos: 0\nflags: 02\n", seen, totals);

            Assert.Single(totals.EngineNsByClient);
            Assert.Equal(500D, totals.EngineNsByClient["2"]);
            Assert.Equal(1024D * 1024D, totals.ResidentMemoryBytes);
            Assert.Equal(2048D, DrmMsmBackend.ParseSize("2 KiB"));
            Assert.Equal(3D * 1024 * 1024 * 1024, DrmMsmBackend.ParseSize("3 GiB"));
            Assert.Equal(12D, DrmMsmBackend.ParseSize("12"));
        }

        /// <summary>
        /// The kgsl backend reads downstream Qualcomm kernels.
        /// </summary>
        [Fact]
        public void KgslShouldReadDownstreamKernel()
        {
            using (TempTree tree = new TempTree())
            {
                tree.Write("class/kgsl/kgsl-3d0/gpu_busy_percentage", "37 %");
                tree.Write("class/kgsl/kgsl-3d0/gpuclk", "585000000");
                tree.Write("class/kgsl/kgsl-3d0/max_gpuclk", "900000000");
                tree.Write("class/kgsl/kgsl-3d0/temp", "48000");
                tree.Write("class/kgsl/kgsl-3d0/gpu_model", "Adreno740v2");

                KgslBackend backend = new KgslBackend(tree.Root);
                QualcommGpuDeviceTelemetry device = Assert.Single(backend.ReadDevices());

                Assert.Equal("Qualcomm Adreno 740", device.Model);
                Assert.Equal(37D, device.Metrics.GpuUtilizationPercent);
                Assert.Equal(585D, device.CurrentFrequencyMHz);
                Assert.Equal(900D, device.MaxFrequencyMHz);
                Assert.Equal(48D, device.Metrics.TemperatureCelsius);
                Assert.Equal(GpuUtilizationScopeEnum.Device, device.UtilizationScope);
                Assert.Equal(GpuTelemetrySourceEnum.Kgsl, device.Source);
            }
        }

        /// <summary>
        /// Adreno display names come from compatible strings and kgsl models.
        /// </summary>
        [Theory]
        [InlineData("qcom,adreno-630.2\0qcom,adreno", "Qualcomm Adreno 630")]
        [InlineData("qcom,adreno-43050a01", "Qualcomm Adreno GPU (chip 0x43050a01)")]
        [InlineData("vendor,other", "Qualcomm Adreno GPU")]
        public void AdrenoNamesShouldComeFromCompatible(string compatible, string expected)
        {
            Assert.Equal(expected, AdrenoModelNames.FromCompatible(compatible));
        }

        /// <summary>
        /// Linux auto mode prefers drm/msm, then kgsl; forced sources and platforms are honored.
        /// </summary>
        [Fact]
        public async Task LinuxResolutionShouldPreferDrmMsmThenKgsl()
        {
            using (TempTree tree = new TempTree())
            {
                tree.Write("class/kgsl/kgsl-3d0/gpu_busy_percentage", "10");
                TelemetrySettings settings = new TelemetrySettings { QualcommSysfsRoot = tree.Root, QualcommProcRoot = tree.Root + "/proc" };

                GpuProbeResult kgsl = await new QualcommGpuTelemetryProvider(settings, HostPlatformEnum.Linux).ProbeAsync(CancellationToken.None);
                Assert.Equal(GpuTelemetrySourceEnum.Kgsl, kgsl.Source);

                tree.Write("class/devfreq/3d00000.gpu/cur_freq", "300000000");
                tree.Write("class/devfreq/3d00000.gpu/device/of_node/compatible", "qcom,adreno-750.1");
                QualcommGpuTelemetryProvider provider = new QualcommGpuTelemetryProvider(settings, HostPlatformEnum.Linux);
                GpuProbeResult msm = await provider.ProbeAsync(CancellationToken.None);
                Assert.Equal(GpuTelemetrySourceEnum.DrmMsm, msm.Source);
                Assert.Equal("Qualcomm Adreno 750", (await provider.GetTelemetryAsync(CancellationToken.None))!.Devices.Single().Model);

                settings.QualcommSource = "kgsl";
                Assert.Equal(GpuTelemetrySourceEnum.Kgsl, (await new QualcommGpuTelemetryProvider(settings, HostPlatformEnum.Linux).ProbeAsync(CancellationToken.None)).Source);

                Assert.False((await new QualcommGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Mac).ProbeAsync(CancellationToken.None)).Available);
                Assert.False((await new QualcommGpuTelemetryProvider(new TelemetrySettings { QualcommEnabled = false }, HostPlatformEnum.Linux).ProbeAsync(CancellationToken.None)).Available);
            }
        }

        private static WindowsGpuAdapterReading AdrenoReading()
        {
            return new WindowsGpuAdapterReading
            {
                VendorId = PciVendorIds.Qualcomm,
                DeviceId = 0x0C36,
                Description = "Snapdragon(R) X Elite - X1E78100 - Qualcomm(R) Adreno(TM) GPU",
                Luid = "0x00000000_0x0000D1B6",
                DriverVersion = "31.0.112.0",
                DedicatedVideoMemoryBytes = 512D * 1024 * 1024,
                SharedSystemMemoryBytes = 16D * 1024 * 1024 * 1024,
                LocalBudgetBytes = 512D * 1024 * 1024,
                NonLocalBudgetBytes = 16D * 1024 * 1024 * 1024,
                LocalUsageBytes = 128D * 1024 * 1024,
                NonLocalUsageBytes = 2D * 1024 * 1024 * 1024,
                EngineUtilization = { { "3d", 41D }, { "compute", 12D } },
                CurrentFrequencyMHz = 1250D,
                MaxFrequencyMHz = 1500D,
                TemperatureCelsius = 52.5D
            };
        }

        private static void WriteFdinfo(TempTree tree, string pid, string fd, int clientId, long engineNs, string memory)
        {
            tree.Write(
                "proc/" + pid + "/fdinfo/" + fd,
                "pos:\t0\nflags:\t02100002\ndrm-driver:\tmsm\ndrm-client-id:\t" + clientId + "\ndrm-engine-gpu:\t" + engineNs + " ns\ndrm-resident-memory:\t" + memory);
        }

        private static KeyValuePair<string, double> Pair(string key, double value)
        {
            return new KeyValuePair<string, double>(key, value);
        }

        private sealed class FakeWindowsNative : IWindowsGpuNative
        {
            public bool CountersOpened { get; private set; } = false;

            public bool Disposed { get; private set; } = false;

            public IReadOnlyList<WindowsGpuAdapterReading> EnumerateAdapters()
            {
                WindowsGpuAdapterReading reading = AdrenoReading();
                reading.EngineUtilization.Clear();
                reading.LocalUsageBytes = null;
                reading.NonLocalUsageBytes = null;
                return new List<WindowsGpuAdapterReading> { reading };
            }

            public bool TryOpenCounters(out string? error)
            {
                CountersOpened = true;
                error = null;
                return true;
            }

            public WindowsPdhSample Collect()
            {
                return new WindowsPdhSample
                {
                    Engines = { Pair("pid_9_luid_0x00000000_0x0000d1b6_phys_0_eng_0_engtype_3D", 64D) },
                    DedicatedUsage = { Pair("luid_0x00000000_0x0000d1b6_phys_0", 128D * 1024 * 1024) },
                    SharedUsage = { Pair("luid_0x00000000_0x0000d1b6_phys_0", 2D * 1024 * 1024 * 1024) }
                };
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
