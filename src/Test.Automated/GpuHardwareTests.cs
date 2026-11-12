namespace Test.Automated
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Amd;
    using RigMonitor.Telemetry.Gpu.Apple;
    using RigMonitor.Telemetry.Gpu.Qualcomm;
    using RigMonitor.Telemetry.Gpu.Windows;
    using RigMonitor.Telemetry.Platform.Mac.Native;
    using RigMonitor.Telemetry.Platform.Windows.Native;

    /// <summary>
    /// Fact that runs only on macOS.
    /// </summary>
    public sealed class MacOSFactAttribute : FactAttribute
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public MacOSFactAttribute()
        {
            if (!OperatingSystem.IsMacOS()) Skip = "Requires macOS.";
        }
    }

    /// <summary>
    /// Fact that runs only on Linux hosts with an amdgpu card.
    /// </summary>
    public sealed class LinuxAmdFactAttribute : FactAttribute
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public LinuxAmdFactAttribute()
        {
            if (!OperatingSystem.IsLinux() || !new AmdSysfsBackend("/sys/class/drm").HasDevices()) Skip = "Requires Linux with an AMD GPU.";
        }
    }

    /// <summary>
    /// Fact that runs only on Linux hosts with an Adreno GPU.
    /// </summary>
    public sealed class LinuxAdrenoFactAttribute : FactAttribute
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public LinuxAdrenoFactAttribute()
        {
            if (!OperatingSystem.IsLinux() || (!new DrmMsmBackend("/sys", "/proc").HasDevices() && !new KgslBackend("/sys").HasDevices()))
            {
                Skip = "Requires Linux with a Qualcomm Adreno GPU.";
            }
        }
    }

    /// <summary>
    /// Fact that runs only on Windows.
    /// </summary>
    public sealed class WindowsFactAttribute : FactAttribute
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public WindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Requires Windows.";
        }
    }

    /// <summary>
    /// Native GPU tests against real hardware. Each test skips itself when its hardware is absent.
    /// </summary>
    [Trait("Category", "Hardware")]
    public class GpuHardwareTests
    {
        /// <summary>
        /// The Apple native layer reads real IOKit, Metal, sysctl, Foundation, IOReport, and IOHID data.
        /// </summary>
        [MacOSFact]
        public async Task AppleNativeLayerShouldReadRealHardware()
        {
            if (!OperatingSystem.IsMacOS()) return;

            using (AppleGpuNative native = new AppleGpuNative())
            {
                AppleAcceleratorInfo accelerator = Assert.Single(native.ReadAccelerators().Take(1));
                Assert.False(String.IsNullOrWhiteSpace(accelerator.IOClass));

                AppleGpuStaticInfo info = native.ReadStatic();
                Assert.True(info.HostMemoryBytes > 0D);

                if (accelerator.IOClass.StartsWith("AGX", StringComparison.Ordinal))
                {
                    Assert.True(accelerator.CoreCount > 0);
                    Assert.True(accelerator.PerformanceStatistics.ContainsKey("Device Utilization %"));
                    Assert.True(info.HasUnifiedMemory);
                    Assert.True(info.RecommendedMaxWorkingSetBytes > 0D);
                    Assert.StartsWith("apple", info.MetalFamily);
                    Assert.True(info.GpuFrequenciesHz.Count(value => value > 0D) > 0);
                }

                int? thermal = native.ReadThermalState();
                Assert.InRange(thermal ?? -1, 0, 3);
                Assert.NotNull(native.ReadWiredLimitMegabytes());

                if (native.TryStartIOReport(out string? _))
                {
                    native.SampleIOReport();
                    await Task.Delay(300);
                    AppleIOReportDelta? delta = native.SampleIOReport();
                    Assert.NotNull(delta);
                    Assert.True(delta.ElapsedSeconds > 0D);
                }
            }
        }

        /// <summary>
        /// The Apple provider produces a populated section on real hardware.
        /// </summary>
        [MacOSFact]
        public async Task AppleProviderShouldProduceSection()
        {
            using (AppleGpuTelemetryProvider provider = new AppleGpuTelemetryProvider(new TelemetrySettings(), HostPlatformEnum.Mac))
            {
                GpuProbeResult probe = await provider.ProbeAsync(CancellationToken.None);
                Assert.True(probe.Available);
                await Task.Delay(1200);
                provider.SampleOnce();

                AppleGpuTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);
                Assert.NotNull(telemetry);
                AppleGpuDeviceTelemetry device = Assert.Single(telemetry.Devices.Take(1));
                Assert.InRange(device.Metrics.GpuUtilizationPercent, 0D, 100D);
                Assert.True(device.Metrics.MemoryTotalMegabytes > 0D);
                if (device.Vendor == GpuVendorEnum.Apple)
                {
                    Assert.True(device.Metrics.MemoryUsedMegabytes > 0D);
                    Assert.True(telemetry.RecommendedMaxWorkingSetMegabytes > 0D);
                    Assert.NotNull(telemetry.ThermalState);
                }
            }
        }

        /// <summary>
        /// The Apple native layer does not leak across repeated reads.
        /// </summary>
        [MacOSFact]
        public void AppleNativeLayerShouldNotLeak()
        {
            if (!OperatingSystem.IsMacOS()) return;

            using (AppleGpuNative native = new AppleGpuNative())
            {
                native.TryStartIOReport(out string? _);
                for (int i = 0; i < 20; i++)
                {
                    native.ReadAccelerators();
                    native.ReadHidTemperatures(AppleGpuMath.IsCandidateSensor);
                    native.SampleIOReport();
                }

                GC.Collect();
                long before = Environment.WorkingSet;
                for (int i = 0; i < 1000; i++)
                {
                    native.ReadAccelerators();
                    native.ReadThermalState();
                    native.SampleIOReport();
                    if (i % 10 == 0)
                    {
                        native.ReadHidTemperatures(AppleGpuMath.IsCandidateSensor);
                    }
                }

                GC.Collect();
                long growth = Environment.WorkingSet - before;
                Assert.True(growth < 32L * 1024L * 1024L, "Working set grew by " + growth + " bytes.");
            }
        }

        /// <summary>
        /// The amdgpu sysfs backend reads real cards.
        /// </summary>
        [LinuxAmdFact]
        public void AmdSysfsShouldReadRealCards()
        {
            AmdGpuDeviceTelemetry device = new AmdSysfsBackend("/sys/class/drm").ReadDevices().First();
            Assert.InRange(device.Metrics.GpuUtilizationPercent, 0D, 100D);
            Assert.True(device.Metrics.MemoryTotalMegabytes > 0D);
        }

        /// <summary>
        /// The Linux Adreno backends read a real GPU.
        /// </summary>
        [LinuxAdrenoFact]
        public void AdrenoShouldReadRealGpu()
        {
            DrmMsmBackend msm = new DrmMsmBackend("/sys", "/proc");
            QualcommGpuDeviceTelemetry device = msm.HasDevices()
                ? msm.ReadDevices(DateTime.UtcNow).First()
                : new KgslBackend("/sys").ReadDevices().First();
            Assert.False(String.IsNullOrWhiteSpace(device.Model));
        }

        /// <summary>
        /// DXGI and PDH interop works on Windows (any adapter, including WARP-only hosts).
        /// </summary>
        [WindowsFact]
        public void WindowsInteropShouldEnumerateAndSample()
        {
            if (!OperatingSystem.IsWindows()) return;

            using (WindowsGpuNative native = new WindowsGpuNative())
            {
                foreach (WindowsGpuAdapterReading adapter in native.EnumerateAdapters())
                {
                    Assert.False(String.IsNullOrWhiteSpace(adapter.Luid));
                    Assert.True(adapter.VendorId != 0U);
                }

                if (native.TryOpenCounters(out string? _))
                {
                    WindowsPdhSample sample = native.Collect();
                    Assert.NotNull(sample.Engines);
                }
            }
        }
    }
}
