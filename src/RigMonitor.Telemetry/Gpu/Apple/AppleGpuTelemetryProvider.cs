namespace RigMonitor.Telemetry.Gpu.Apple
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Platform.Mac.Native;

    /// <summary>
    /// Latest values produced by the Apple IOReport background sampler.
    /// </summary>
    public sealed class AppleSamplerReading
    {
        /// <summary>
        /// When the reading was taken.
        /// </summary>
        public DateTime SampledUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// GPU power in watts.
        /// </summary>
        public double? PowerWatts { get; set; } = null;

        /// <summary>
        /// Weighted active frequency in MHz.
        /// </summary>
        public double? FrequencyMHz { get; set; } = null;

        /// <summary>
        /// Active residency percent.
        /// </summary>
        public double? ActiveResidencyPercent { get; set; } = null;

        /// <summary>
        /// Selected GPU temperature sensor values in Celsius.
        /// </summary>
        public List<double> TemperaturesCelsius { get; set; } = new List<double>();

        /// <summary>
        /// What the temperatures measure (gpuSensors or socDie).
        /// </summary>
        public string? TemperatureSource { get; set; } = null;
    }

    /// <summary>
    /// Apple GPU telemetry provider using in-process macOS frameworks.
    /// </summary>
    public sealed class AppleGpuTelemetryProvider : IAppleGpuTelemetryProvider, IDisposable
    {
        private static readonly TimeSpan _StaticRefresh = TimeSpan.FromHours(1);
        private readonly TelemetrySettings _Settings;
        private readonly HostPlatformEnum _Platform;
        private readonly Func<IAppleGpuNative> _NativeFactory;
        private readonly TimeProvider _TimeProvider;
        private readonly object _Lock = new object();
        private IAppleGpuNative? _Native = null;
        private AppleGpuStaticInfo? _Static = null;
        private DateTime _StaticReadUtc = DateTime.MinValue;
        private bool _IOReport = false;
        private bool _Hid = false;
        private bool _ProcessInfo = false;
        private Timer? _Timer = null;
        private AppleSamplerReading? _Latest = null;
        private bool _Available = false;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="platform">Host platform.</param>
        /// <param name="nativeFactory">Factory for the native seam (defaults to the real macOS binding).</param>
        /// <param name="timeProvider">Time provider.</param>
        public AppleGpuTelemetryProvider(
            TelemetrySettings settings,
            HostPlatformEnum platform,
            Func<IAppleGpuNative>? nativeFactory = null,
            TimeProvider? timeProvider = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Platform = platform;
            _NativeFactory = nativeFactory ?? CreateDefaultNative;
            _TimeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Vendor served by this provider.
        /// </summary>
        public GpuVendorEnum Vendor
        {
            get
            {
                return GpuVendorEnum.Apple;
            }
        }

        /// <summary>
        /// Native features that loaded.
        /// </summary>
        public List<string> Features
        {
            get
            {
                List<string> features = new List<string>();
                if (_Available) features.Add("ioKit");
                if (_Static?.MetalDeviceName != null) features.Add("metal");
                if (_ProcessInfo) features.Add("processInfo");
                if (_IOReport) features.Add("ioReport");
                if (_Hid) features.Add("hidSensors");
                return features;
            }
        }

        /// <summary>
        /// Probe the native frameworks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Probe result.</returns>
        public Task<GpuProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            if (!_Settings.AppleGpuEnabled)
            {
                return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Apple, "Apple GPU telemetry is disabled in settings."));
            }

            if (_Platform != HostPlatformEnum.Mac)
            {
                return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Apple, "Apple GPU telemetry requires macOS."));
            }

            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceAppleNative, "ioKit", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    IAppleGpuNative native = EnsureNative();
                    List<AppleAcceleratorInfo> accelerators = native.ReadAccelerators();
                    if (accelerators.Count < 1)
                    {
                        call.Fail(RigMonitorTelemetryNames.OutcomeUnavailable, "No IOAccelerator entries.");
                        return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Apple, "IOKit reports no GPU accelerators."));
                    }

                    call.Succeed();
                    _Available = true;
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    RigMonitorInstrumentation.RecordGpuNativeError("apple", "ioKit");
                    return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Apple, "Apple native frameworks failed to load: " + exception.Message));
                }
            }

            ReadStaticIfDue(true);
            _ProcessInfo = SafeRead(() => _Native!.ReadThermalState(), "processInfo").HasValue;

            if (_Settings.AppleIOReportEnabled)
            {
                string? error = null;
                _IOReport = SafeRead(() => (bool?)_Native!.TryStartIOReport(out error), "ioReport") == true;
            }

            if (_Settings.AppleHidSensorsEnabled)
            {
                List<KeyValuePair<string, double>>? sensors = SafeRead(() => _Native!.ReadHidTemperatures(AppleGpuMath.IsCandidateSensor), "hidSensors");
                _Hid = AppleGpuMath.SelectGpuSensors(sensors, out string? _).Count > 0;
            }

            if (_IOReport || _Hid)
            {
                StartSampler();
            }

            RigMonitorInstrumentation.SetGpuNativeFeature("ioKit", true);
            RigMonitorInstrumentation.SetGpuNativeFeature("metal", _Static?.MetalDeviceName != null);
            RigMonitorInstrumentation.SetGpuNativeFeature("processInfo", _ProcessInfo);
            RigMonitorInstrumentation.SetGpuNativeFeature("ioReport", _IOReport);
            RigMonitorInstrumentation.SetGpuNativeFeature("hidSensors", _Hid);

            GpuProbeResult result = new GpuProbeResult
            {
                Vendor = GpuVendorEnum.Apple,
                Available = true,
                Source = GpuTelemetrySourceEnum.AppleNative,
                Endpoint = "ioKit",
                Detail = _Static?.MetalDeviceName,
                Features = Features
            };

            return Task.FromResult(result);
        }

        /// <summary>
        /// Capture the Apple section.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Apple section, or null when unavailable.</returns>
        public Task<AppleGpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            if (!_Available || _Native == null)
            {
                return Task.FromResult<AppleGpuTelemetry?>(null);
            }

            List<AppleAcceleratorInfo> accelerators;
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceAppleNative, "ioKit", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    accelerators = _Native.ReadAccelerators();
                    call.Succeed();
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    RigMonitorInstrumentation.RecordGpuNativeError("apple", "ioKit");
                    throw;
                }
            }

            ReadStaticIfDue(false);
            int? thermal = _ProcessInfo ? SafeRead(() => _Native.ReadThermalState(), "processInfo") : null;
            long? wiredLimit = SafeRead(() => _Native.ReadWiredLimitMegabytes(), "sysctl");
            AppleSamplerReading? sampler = CurrentSamplerReading();
            List<double> gpuSensors = sampler?.TemperaturesCelsius ?? new List<double>();

            AppleGpuTelemetry? telemetry = Build(accelerators, _Static, thermal, wiredLimit, gpuSensors, sampler, Features);
            if (telemetry != null && gpuSensors.Count > 0)
            {
                telemetry.TemperatureSource = sampler?.TemperatureSource;
            }
            return Task.FromResult(telemetry);
        }

        /// <summary>
        /// Build the Apple section from native readings.
        /// </summary>
        /// <param name="accelerators">IOAccelerator entries.</param>
        /// <param name="staticInfo">Static info.</param>
        /// <param name="thermalState">Thermal state.</param>
        /// <param name="wiredLimitMegabytes">iogpu.wired_limit_mb.</param>
        /// <param name="gpuSensorsCelsius">GPU sensor temperatures.</param>
        /// <param name="sampler">Latest IOReport reading.</param>
        /// <param name="features">Active native features.</param>
        /// <returns>Apple section, or null when there are no accelerators.</returns>
        public static AppleGpuTelemetry? Build(
            List<AppleAcceleratorInfo> accelerators,
            AppleGpuStaticInfo? staticInfo,
            int? thermalState,
            long? wiredLimitMegabytes,
            List<double> gpuSensorsCelsius,
            AppleSamplerReading? sampler,
            List<string> features)
        {
            if (accelerators == null || accelerators.Count < 1)
            {
                return null;
            }

            double? hostMemoryMegabytes = staticInfo?.HostMemoryBytes.HasValue == true ? GpuMetricMath.BytesToMegabytes(staticInfo.HostMemoryBytes.Value) : null;
            double? workingSetMegabytes = staticInfo?.RecommendedMaxWorkingSetBytes.HasValue == true ? GpuMetricMath.BytesToMegabytes(staticInfo.RecommendedMaxWorkingSetBytes.Value) : null;
            GpuThermalStateEnum? thermal = thermalState.HasValue ? (GpuThermalStateEnum)thermalState.Value : null;
            double? averageTemperature = gpuSensorsCelsius != null && gpuSensorsCelsius.Count > 0 ? gpuSensorsCelsius.Average() : null;
            double? maxTemperature = gpuSensorsCelsius != null && gpuSensorsCelsius.Count > 0 ? gpuSensorsCelsius.Max() : null;

            AppleGpuTelemetry telemetry = new AppleGpuTelemetry
            {
                Source = GpuTelemetrySourceEnum.AppleNative,
                ThermalState = thermal,
                HostMemoryTotalMegabytes = hostMemoryMegabytes,
                RecommendedMaxWorkingSetMegabytes = workingSetMegabytes,
                WiredLimitMegabytes = wiredLimitMegabytes.HasValue && wiredLimitMegabytes.Value > 0L ? wiredLimitMegabytes.Value : null,
                MetalFamily = staticInfo?.MetalFamily,
                Features = features ?? new List<string>()
            };

            int index = 0;
            foreach (AppleAcceleratorInfo accelerator in accelerators)
            {
                string vendor = AppleGpuMath.VendorForAccelerator(accelerator.IOClass, accelerator.Model);
                bool appleSilicon = vendor == "apple";
                AppleGpuDeviceTelemetry device = new AppleGpuDeviceTelemetry
                {
                    DeviceIndex = index,
                    Vendor = vendor == "amd" ? GpuVendorEnum.Amd : (vendor == "intel" ? GpuVendorEnum.Intel : (vendor == "nvidia" ? GpuVendorEnum.Nvidia : GpuVendorEnum.Apple)),
                    Model = !String.IsNullOrWhiteSpace(accelerator.Model) ? accelerator.Model : (staticInfo?.MetalDeviceName ?? "Apple GPU"),
                    Uuid = accelerator.RegistryName + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    BusId = accelerator.IOClass,
                    DriverVersion = Environment.OSVersion.VersionString,
                    UnifiedMemory = appleSilicon,
                    GpuCoreCount = accelerator.CoreCount,
                    ThermalState = thermal
                };

                Dictionary<string, double> statistics = accelerator.PerformanceStatistics;
                double? utilization = Read(statistics, "Device Utilization %") ?? Read(statistics, "GPU Activity(%)");
                if (utilization.HasValue)
                {
                    device.Metrics.GpuUtilizationPercent = utilization.Value;
                }

                device.RendererUtilizationPercent = Read(statistics, "Renderer Utilization %");
                device.TilerUtilizationPercent = Read(statistics, "Tiler Utilization %");

                double? allocated = Read(statistics, "Alloc system memory");
                device.AllocatedSystemMemoryMegabytes = allocated.HasValue ? GpuMetricMath.BytesToMegabytes(allocated.Value) : null;

                if (appleSilicon)
                {
                    double? inUse = Read(statistics, "In use system memory");
                    device.Metrics.MemoryUsedMegabytes = inUse.HasValue ? GpuMetricMath.BytesToMegabytes(inUse.Value) : 0D;
                    if (hostMemoryMegabytes.HasValue)
                    {
                        device.Metrics.MemoryTotalMegabytes = hostMemoryMegabytes.Value;
                        device.Metrics.MemoryFreeMegabytes = Math.Max(0D, hostMemoryMegabytes.Value - device.Metrics.MemoryUsedMegabytes);
                    }

                    device.Metrics.MemorySource = "unifiedSystemMemory";
                    device.Metrics.MemoryShared = true;
                    device.RecommendedMaxWorkingSetMegabytes = workingSetMegabytes;
                    device.Metrics.TemperatureCelsius = averageTemperature;
                    device.MaxTemperatureCelsius = maxTemperature;

                    if (sampler != null)
                    {
                        device.Metrics.PowerUsageWatts = sampler.PowerWatts;
                        device.Metrics.SmClockMHz = sampler.FrequencyMHz;
                        device.ActiveResidencyPercent = sampler.ActiveResidencyPercent;
                    }

                    if (accelerator.CoreCount.HasValue && !telemetry.GpuCoreCount.HasValue)
                    {
                        telemetry.GpuCoreCount = accelerator.CoreCount;
                    }

                    if (String.IsNullOrWhiteSpace(telemetry.Chip))
                    {
                        telemetry.Chip = device.Model;
                    }
                }
                else
                {
                    double? vramUsed = Read(statistics, "vramUsedBytes");
                    double? vramFree = Read(statistics, "vramFreeBytes");
                    if (vramUsed.HasValue)
                    {
                        device.Metrics.MemoryUsedMegabytes = GpuMetricMath.BytesToMegabytes(vramUsed.Value);
                        device.Metrics.MemoryFreeMegabytes = GpuMetricMath.BytesToMegabytes(vramFree ?? 0D);
                        device.Metrics.MemorySource = "vram";
                        device.Metrics.MemoryShared = false;
                    }
                }

                telemetry.Devices.Add(device);
                index++;
            }

            if (String.IsNullOrWhiteSpace(telemetry.Chip))
            {
                telemetry.Chip = staticInfo?.MetalDeviceName ?? telemetry.Devices[0].Model;
            }

            return telemetry;
        }

        /// <summary>
        /// Compute a sampler reading from an IOReport delta.
        /// </summary>
        /// <param name="delta">Delta.</param>
        /// <param name="frequenciesHz">DVFS table.</param>
        /// <param name="sampledUtc">Sample time.</param>
        /// <returns>Reading.</returns>
        public static AppleSamplerReading ComputeReading(AppleIOReportDelta delta, IReadOnlyList<double> frequenciesHz, DateTime sampledUtc)
        {
            AppleSamplerReading reading = new AppleSamplerReading { SampledUtc = sampledUtc };
            if (delta.GpuEnergyJoules.HasValue && delta.ElapsedSeconds > 0D)
            {
                reading.PowerWatts = Math.Max(0D, delta.GpuEnergyJoules.Value / delta.ElapsedSeconds);
            }

            AppleGpuMath.ComputeFrequency(delta.GpuStateResidencies, frequenciesHz, out double? frequency, out double? residency);
            reading.FrequencyMHz = frequency;
            reading.ActiveResidencyPercent = residency;
            return reading;
        }

        /// <summary>
        /// Take one IOReport sample synchronously (used by the background timer and tests).
        /// </summary>
        public void SampleOnce()
        {
            IAppleGpuNative? native = _Native;
            if (native == null || (!_IOReport && !_Hid))
            {
                return;
            }

            DateTime now = _TimeProvider.GetUtcNow().UtcDateTime;
            AppleSamplerReading reading = new AppleSamplerReading { SampledUtc = now };
            bool hasData = false;

            if (_IOReport)
            {
                try
                {
                    AppleIOReportDelta? delta = native.SampleIOReport();
                    if (delta != null)
                    {
                        AppleSamplerReading computed = ComputeReading(delta, _Static?.GpuFrequenciesHz ?? new List<double>(), now);
                        reading.PowerWatts = computed.PowerWatts;
                        reading.FrequencyMHz = computed.FrequencyMHz;
                        reading.ActiveResidencyPercent = computed.ActiveResidencyPercent;
                        hasData = true;
                    }
                }
                catch (Exception)
                {
                    RigMonitorInstrumentation.RecordGpuNativeError("apple", "ioReport");
                }
            }

            if (_Hid)
            {
                List<KeyValuePair<string, double>>? sensors = SafeRead(() => native.ReadHidTemperatures(AppleGpuMath.IsCandidateSensor), "hidSensors");
                reading.TemperaturesCelsius = AppleGpuMath.SelectGpuSensors(sensors, out string? source);
                reading.TemperatureSource = source;
                hasData |= reading.TemperaturesCelsius.Count > 0;
            }

            if (!hasData)
            {
                return;
            }

            lock (_Lock)
            {
                _Latest = reading;
            }
        }

        /// <summary>
        /// Stop the sampler and release native resources.
        /// </summary>
        public void Dispose()
        {
            lock (_Lock)
            {
                _Timer?.Dispose();
                _Timer = null;
            }

            _Native?.Dispose();
        }

        private AppleSamplerReading? CurrentSamplerReading()
        {
            lock (_Lock)
            {
                if (_Latest == null)
                {
                    return null;
                }

                TimeSpan age = _TimeProvider.GetUtcNow().UtcDateTime - _Latest.SampledUtc;
                return age <= TimeSpan.FromMilliseconds(_Settings.AppleSamplerIntervalMs * 3D + 1000D) ? _Latest : null;
            }
        }

        private void StartSampler()
        {
            SampleOnce();
            lock (_Lock)
            {
                _Timer ??= new Timer(_ => SampleOnce(), null, _Settings.AppleSamplerIntervalMs, _Settings.AppleSamplerIntervalMs);
            }
        }

        private void ReadStaticIfDue(bool force)
        {
            DateTime now = _TimeProvider.GetUtcNow().UtcDateTime;
            if (!force && _Static != null && now - _StaticReadUtc < _StaticRefresh)
            {
                return;
            }

            AppleGpuStaticInfo? info = SafeRead(() => _Native?.ReadStatic(), "metal");
            if (info != null)
            {
                _Static = info;
                _StaticReadUtc = now;
            }
        }

        private IAppleGpuNative EnsureNative()
        {
            lock (_Lock)
            {
                _Native ??= _NativeFactory();
                return _Native;
            }
        }

        private static T? SafeRead<T>(Func<T?> read, string feature)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                RigMonitorInstrumentation.RecordGpuNativeError("apple", feature);
                return default;
            }
        }

        private static double? Read(Dictionary<string, double> statistics, string key)
        {
            return statistics != null && statistics.TryGetValue(key, out double value) ? value : null;
        }

        private static IAppleGpuNative CreateDefaultNative()
        {
            if (!OperatingSystem.IsMacOS())
            {
                throw new PlatformNotSupportedException("Apple GPU telemetry requires macOS.");
            }

            return new AppleGpuNative();
        }
    }
}
