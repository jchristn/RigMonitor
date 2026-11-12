namespace RigMonitor.Telemetry.Gpu.Amd
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
    using RigMonitor.Telemetry.Gpu.Amd.Native;
    using RigMonitor.Telemetry.Gpu.Prometheus;
    using RigMonitor.Telemetry.Gpu.Windows;

    /// <summary>
    /// AMD GPU telemetry provider. On Linux it prefers the native AMD SMI library, fills gaps from amdgpu sysfs by PCI address,
    /// falls back to sysfs alone, and uses the AMD Device Metrics Exporter last. On Windows it uses the graphics kernel backend.
    /// </summary>
    public sealed class AmdGpuTelemetryProvider : IAmdGpuTelemetryProvider, IDisposable
    {
        private readonly TelemetrySettings _Settings;
        private readonly IAmdSmiNative? _Native;
        private readonly AmdSysfsBackend _Sysfs;
        private readonly IPrometheusTextClient _Exporter;
        private readonly IWindowsGpuAdapterSource? _Windows;
        private readonly HostPlatformEnum _Platform;
        private GpuTelemetrySourceEnum? _ResolvedSource = null;
        private bool _GapFill = false;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="platform">Host platform.</param>
        /// <param name="native">AMD SMI library seam (Linux).</param>
        /// <param name="sysfs">amdgpu sysfs backend (Linux).</param>
        /// <param name="exporter">AMD Device Metrics Exporter client.</param>
        /// <param name="windows">Windows adapter source.</param>
        public AmdGpuTelemetryProvider(
            TelemetrySettings settings,
            HostPlatformEnum platform,
            IAmdSmiNative? native = null,
            AmdSysfsBackend? sysfs = null,
            IPrometheusTextClient? exporter = null,
            IWindowsGpuAdapterSource? windows = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Platform = platform;
            _Native = native;
            _Sysfs = sysfs ?? new AmdSysfsBackend(settings.AmdSysfsRoot);
            _Exporter = exporter ?? new PrometheusTextClient(
                RigMonitorTelemetryNames.ServiceAmdMetricsExporter,
                "AMD Device Metrics Exporter",
                settings.AmdMetricsExporterUrl,
                settings.RequestTimeoutMs);
            _Windows = windows;
        }

        /// <summary>
        /// Vendor served by this provider.
        /// </summary>
        public GpuVendorEnum Vendor
        {
            get
            {
                return GpuVendorEnum.Amd;
            }
        }

        /// <summary>
        /// Resolved primary source after probing.
        /// </summary>
        public GpuTelemetrySourceEnum? ResolvedSource
        {
            get
            {
                return _ResolvedSource;
            }
        }

        /// <summary>
        /// Resolve the AMD source for this host.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Probe result.</returns>
        public async Task<GpuProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            _ResolvedSource = null;
            _GapFill = false;

            if (!_Settings.AmdEnabled)
            {
                return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, "AMD telemetry is disabled in settings.");
            }

            if (_Platform == HostPlatformEnum.Windows)
            {
                return ProbeWindows();
            }

            if (_Platform != HostPlatformEnum.Linux)
            {
                return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, "AMD telemetry is supported on Linux and Windows.");
            }

            string mode = _Settings.AmdSource;
            List<string> reasons = new List<string>();

            if (mode == "auto" || mode == "native")
            {
                GpuProbeResult? native = ProbeNative(reasons);
                if (native != null) return native;
                if (mode == "native") return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, String.Join(" ", reasons));
            }

            if (mode == "auto" || mode == "sysfs")
            {
                using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceAmdSysfs, "probe", null, System.Diagnostics.ActivityKind.Internal))
                {
                    if (_Sysfs.HasDevices())
                    {
                        call.Succeed();
                        _ResolvedSource = GpuTelemetrySourceEnum.AmdSysfs;
                        RigMonitorInstrumentation.SetGpuNativeFeature("amdSysfs", true);
                        return Available(GpuTelemetrySourceEnum.AmdSysfs, _Sysfs.Root, null);
                    }

                    call.Fail(RigMonitorTelemetryNames.OutcomeUnavailable, "No amdgpu devices under " + _Sysfs.Root + ".");
                }

                reasons.Add("No amdgpu devices were found under " + _Sysfs.Root + ".");
                if (mode == "sysfs") return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, String.Join(" ", reasons));
            }

            string? metrics = await _Exporter.TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            if (AmdMetricsExporterParser.ContainsAmdMetrics(metrics))
            {
                _ResolvedSource = GpuTelemetrySourceEnum.AmdMetricsExporter;
                return Available(GpuTelemetrySourceEnum.AmdMetricsExporter, _Exporter.Endpoint, null);
            }

            reasons.Add("AMD Device Metrics Exporter is not reachable at " + _Exporter.Endpoint + ".");
            return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, String.Join(" ", reasons));
        }

        /// <summary>
        /// Capture the AMD section.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>AMD section, or null when no current sample is available.</returns>
        public async Task<AmdGpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            switch (_ResolvedSource)
            {
                case GpuTelemetrySourceEnum.AmdSmiLibrary:
                    return await Task.Run(() => ReadNative(), cancellationToken).ConfigureAwait(false);
                case GpuTelemetrySourceEnum.AmdSysfs:
                    return await Task.Run(() => ReadSysfs(), cancellationToken).ConfigureAwait(false);
                case GpuTelemetrySourceEnum.AmdMetricsExporter:
                    string? metrics = await _Exporter.TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
                    return Build(GpuTelemetrySourceEnum.AmdMetricsExporter, null, null, _Exporter.Endpoint, AmdMetricsExporterParser.Parse(metrics, cancellationToken));
                case GpuTelemetrySourceEnum.WindowsGraphicsKernel:
                    return ReadWindows();
                default:
                    return null;
            }
        }

        /// <summary>
        /// Release the native library.
        /// </summary>
        public void Dispose()
        {
            _Native?.Dispose();
        }

        /// <summary>
        /// Convert library samples to devices.
        /// </summary>
        /// <param name="samples">Library samples.</param>
        /// <returns>Devices.</returns>
        public static List<AmdGpuDeviceTelemetry> MapLibrarySamples(IEnumerable<AmdSmiDeviceSample> samples)
        {
            List<AmdGpuDeviceTelemetry> devices = new List<AmdGpuDeviceTelemetry>();
            foreach (AmdSmiDeviceSample sample in samples)
            {
                AmdGpuDeviceTelemetry device = new AmdGpuDeviceTelemetry
                {
                    DeviceIndex = sample.Index,
                    Source = GpuTelemetrySourceEnum.AmdSmiLibrary,
                    BusId = AmdSysfsBackend.NormalizeBusId(sample.BusId),
                    Uuid = sample.Uuid,
                    Model = String.IsNullOrWhiteSpace(sample.MarketName) ? String.Empty : sample.MarketName,
                    ComputePartition = sample.ComputePartition,
                    MemoryPartition = sample.MemoryPartition
                };

                const GpuTelemetrySourceEnum library = GpuTelemetrySourceEnum.AmdSmiLibrary;
                if (sample.GfxActivityPercent.HasValue)
                {
                    device.Metrics.GpuUtilizationPercent = sample.GfxActivityPercent.Value;
                    device.FieldSources["utilization"] = library;
                }

                if (sample.UmcActivityPercent.HasValue)
                {
                    device.MemoryActivityPercent = sample.UmcActivityPercent;
                    device.FieldSources["memoryActivity"] = library;
                }

                device.MultimediaActivityPercent = sample.MmActivityPercent;

                if (sample.VramTotalBytes.HasValue && sample.VramTotalBytes.Value > 0D)
                {
                    device.Metrics.MemoryTotalMegabytes = GpuMetricMath.BytesToMegabytes(sample.VramTotalBytes.Value);
                    device.Metrics.MemoryUsedMegabytes = GpuMetricMath.BytesToMegabytes(sample.VramUsedBytes ?? 0D);
                    device.Metrics.MemoryFreeMegabytes = Math.Max(0D, device.Metrics.MemoryTotalMegabytes - device.Metrics.MemoryUsedMegabytes);
                    device.Metrics.MemorySource = "vram";
                    device.Metrics.MemoryShared = false;
                    device.FieldSources["memory"] = library;
                }

                if (sample.GttTotalBytes.HasValue && sample.GttTotalBytes.Value > 0D)
                {
                    device.GttTotalMegabytes = GpuMetricMath.BytesToMegabytes(sample.GttTotalBytes.Value);
                    device.GttUsedMegabytes = sample.GttUsedBytes.HasValue ? GpuMetricMath.BytesToMegabytes(sample.GttUsedBytes.Value) : null;
                    device.FieldSources["gtt"] = library;
                }

                if (sample.EdgeTemperatureCelsius.HasValue)
                {
                    device.Metrics.TemperatureCelsius = sample.EdgeTemperatureCelsius;
                    device.FieldSources["temperature"] = library;
                }

                if (sample.HotspotTemperatureCelsius.HasValue)
                {
                    device.HotspotTemperatureCelsius = sample.HotspotTemperatureCelsius;
                    device.FieldSources["hotspotTemperature"] = library;
                    if (!device.Metrics.TemperatureCelsius.HasValue)
                    {
                        device.Metrics.TemperatureCelsius = sample.HotspotTemperatureCelsius;
                        device.FieldSources["temperature"] = library;
                    }
                }

                if (sample.MemoryTemperatureCelsius.HasValue)
                {
                    device.MemoryTemperatureCelsius = sample.MemoryTemperatureCelsius;
                    device.FieldSources["memoryTemperature"] = library;
                }

                if (sample.GfxClockMHz.HasValue)
                {
                    device.Metrics.SmClockMHz = sample.GfxClockMHz;
                    device.FieldSources["smClock"] = library;
                }

                if (sample.MemoryClockMHz.HasValue)
                {
                    device.Metrics.MemoryClockMHz = sample.MemoryClockMHz;
                    device.FieldSources["memoryClock"] = library;
                }

                if (sample.CorrectableEccErrors.HasValue || sample.UncorrectableEccErrors.HasValue)
                {
                    device.CorrectableEccErrors = sample.CorrectableEccErrors;
                    device.UncorrectableEccErrors = sample.UncorrectableEccErrors;
                    device.FieldSources["ecc"] = library;
                }

                devices.Add(device);
            }

            return devices;
        }

        /// <summary>
        /// Fill fields the primary device lacks from a sysfs device for the same PCI address.
        /// </summary>
        /// <param name="primary">Primary devices (modified in place).</param>
        /// <param name="secondary">Sysfs devices.</param>
        /// <returns>True when at least one field was filled.</returns>
        public static bool GapFill(List<AmdGpuDeviceTelemetry> primary, List<AmdGpuDeviceTelemetry> secondary)
        {
            bool filled = false;
            Dictionary<string, AmdGpuDeviceTelemetry> byBus = new Dictionary<string, AmdGpuDeviceTelemetry>(StringComparer.OrdinalIgnoreCase);
            foreach (AmdGpuDeviceTelemetry device in secondary)
            {
                if (!String.IsNullOrWhiteSpace(device.BusId))
                {
                    byBus[device.BusId] = device;
                }
            }

            foreach (AmdGpuDeviceTelemetry device in primary)
            {
                if (String.IsNullOrWhiteSpace(device.BusId) || !byBus.TryGetValue(device.BusId, out AmdGpuDeviceTelemetry? source))
                {
                    continue;
                }

                filled |= FillDevice(device, source);
            }

            return filled;
        }

        private static bool FillDevice(AmdGpuDeviceTelemetry target, AmdGpuDeviceTelemetry source)
        {
            bool filled = false;
            const GpuTelemetrySourceEnum sysfs = GpuTelemetrySourceEnum.AmdSysfs;

            bool Missing(string field) => !target.FieldSources.ContainsKey(field) && source.FieldSources.ContainsKey(field);

            if (Missing("utilization"))
            {
                target.Metrics.GpuUtilizationPercent = source.Metrics.GpuUtilizationPercent;
                target.FieldSources["utilization"] = sysfs;
                filled = true;
            }

            if (Missing("memoryActivity"))
            {
                target.MemoryActivityPercent = source.MemoryActivityPercent;
                target.FieldSources["memoryActivity"] = sysfs;
                filled = true;
            }

            if (Missing("memory"))
            {
                target.Metrics.MemoryTotalMegabytes = source.Metrics.MemoryTotalMegabytes;
                target.Metrics.MemoryUsedMegabytes = source.Metrics.MemoryUsedMegabytes;
                target.Metrics.MemoryFreeMegabytes = source.Metrics.MemoryFreeMegabytes;
                target.Metrics.MemorySource = source.Metrics.MemorySource;
                target.Metrics.MemoryShared = source.Metrics.MemoryShared;
                target.FieldSources["memory"] = sysfs;
                filled = true;
            }

            if (Missing("gtt"))
            {
                target.GttTotalMegabytes = source.GttTotalMegabytes;
                target.GttUsedMegabytes = source.GttUsedMegabytes;
                target.FieldSources["gtt"] = sysfs;
                filled = true;
            }

            if (Missing("temperature"))
            {
                target.Metrics.TemperatureCelsius = source.Metrics.TemperatureCelsius;
                target.FieldSources["temperature"] = sysfs;
                filled = true;
            }

            if (Missing("hotspotTemperature"))
            {
                target.HotspotTemperatureCelsius = source.HotspotTemperatureCelsius;
                target.FieldSources["hotspotTemperature"] = sysfs;
                filled = true;
            }

            if (Missing("memoryTemperature"))
            {
                target.MemoryTemperatureCelsius = source.MemoryTemperatureCelsius;
                target.FieldSources["memoryTemperature"] = sysfs;
                filled = true;
            }

            if (Missing("power"))
            {
                target.Metrics.PowerUsageWatts = source.Metrics.PowerUsageWatts;
                target.FieldSources["power"] = sysfs;
                filled = true;
            }

            if (Missing("smClock"))
            {
                target.Metrics.SmClockMHz = source.Metrics.SmClockMHz;
                target.FieldSources["smClock"] = sysfs;
                filled = true;
            }

            if (Missing("memoryClock"))
            {
                target.Metrics.MemoryClockMHz = source.Metrics.MemoryClockMHz;
                target.FieldSources["memoryClock"] = sysfs;
                filled = true;
            }

            if (String.IsNullOrWhiteSpace(target.Model)) target.Model = source.Model;
            if (String.IsNullOrWhiteSpace(target.Uuid)) target.Uuid = source.Uuid;
            if (String.IsNullOrWhiteSpace(target.DriverVersion)) target.DriverVersion = source.DriverVersion;
            return filled;
        }

        /// <summary>
        /// Combine VRAM and GTT on APUs so common memory reflects what the GPU can actually use.
        /// </summary>
        /// <param name="devices">Devices (modified in place).</param>
        public static void ApplyApuMemory(IEnumerable<AmdGpuDeviceTelemetry> devices)
        {
            foreach (AmdGpuDeviceTelemetry device in devices)
            {
                if (!device.UnifiedMemory)
                {
                    double? vramBytes = device.Metrics.MemoryTotalMegabytes > 0D ? device.Metrics.MemoryTotalMegabytes * 1024D * 1024D : null;
                    double? gttBytes = device.GttTotalMegabytes.HasValue ? device.GttTotalMegabytes * 1024D * 1024D : null;
                    device.UnifiedMemory = AmdSysfsBackend.IsApu(vramBytes, gttBytes, device.Model, null);
                }

                if (!device.UnifiedMemory || !device.GttTotalMegabytes.HasValue)
                {
                    continue;
                }

                double total = device.Metrics.MemoryTotalMegabytes + device.GttTotalMegabytes.Value;
                double used = device.Metrics.MemoryUsedMegabytes + (device.GttUsedMegabytes ?? 0D);
                device.Metrics.MemoryTotalMegabytes = total;
                device.Metrics.MemoryUsedMegabytes = used;
                device.Metrics.MemoryFreeMegabytes = Math.Max(0D, total - used);
                device.Metrics.MemorySource = "vramPlusGtt";
                device.Metrics.MemoryShared = true;
            }
        }

        private GpuProbeResult? ProbeNative(List<string> reasons)
        {
            if (_Native == null)
            {
                reasons.Add("The AMD SMI library binding is not available.");
                return null;
            }

            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceAmdSmiLibrary, "init", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    if (!_Native.TryInitialize(out string? error))
                    {
                        call.Fail(RigMonitorTelemetryNames.OutcomeUnavailable, error);
                        reasons.Add(error ?? "The AMD SMI library failed to initialize.");
                        RigMonitorInstrumentation.SetGpuNativeFeature("amdSmiLibrary", false);
                        return null;
                    }

                    if (_Native.Sample().Count < 1)
                    {
                        call.Fail(RigMonitorTelemetryNames.OutcomeUnavailable, "The AMD SMI library reported no GPUs.");
                        reasons.Add("The AMD SMI library reported no GPUs.");
                        return null;
                    }

                    call.Succeed();
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    reasons.Add("The AMD SMI library failed: " + exception.Message);
                    RigMonitorInstrumentation.RecordGpuNativeError("amd", "amdSmiLibrary");
                    return null;
                }
            }

            RigMonitorInstrumentation.SetGpuNativeFeature("amdSmiLibrary", true);
            _ResolvedSource = GpuTelemetrySourceEnum.AmdSmiLibrary;
            _GapFill = _Settings.AmdSysfsGapFillEnabled && _Sysfs.HasDevices();
            RigMonitorInstrumentation.SetGpuNativeFeature("amdSysfs", _GapFill);

            string detail = "lib " + (_Native.LibraryVersion ?? "unknown") + (_GapFill ? ", gap-fill: amdSysfs" : String.Empty);
            return Available(GpuTelemetrySourceEnum.AmdSmiLibrary, _Native.LibraryPath, detail);
        }

        private GpuProbeResult ProbeWindows()
        {
            if (!_Settings.WindowsGenericGpuEnabled || _Windows == null || !_Windows.IsAvailable())
            {
                return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, "AMD telemetry on Windows requires the Windows graphics kernel backend.");
            }

            bool any = _Windows.GetAdapters().Any(adapter => adapter.VendorId == PciVendorIds.Amd);
            if (!any)
            {
                return GpuProbeResult.Unavailable(GpuVendorEnum.Amd, "No AMD adapters were enumerated by DXGI.");
            }

            _ResolvedSource = GpuTelemetrySourceEnum.WindowsGraphicsKernel;
            return Available(GpuTelemetrySourceEnum.WindowsGraphicsKernel, "dxgi", null);
        }

        private static GpuProbeResult Available(GpuTelemetrySourceEnum source, string? endpoint, string? detail)
        {
            return new GpuProbeResult
            {
                Vendor = GpuVendorEnum.Amd,
                Available = true,
                Source = source,
                Endpoint = endpoint,
                Detail = detail
            };
        }

        private AmdGpuTelemetry? ReadNative()
        {
            if (_Native == null)
            {
                return null;
            }

            List<AmdGpuDeviceTelemetry> devices;
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceAmdSmiLibrary, "sample", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    devices = MapLibrarySamples(_Native.Sample());
                    call.Succeed();
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    RigMonitorInstrumentation.RecordGpuNativeError("amd", "amdSmiLibrary");
                    if (_Sysfs.HasDevices())
                    {
                        return ReadSysfs();
                    }

                    throw;
                }
            }

            GpuTelemetrySourceEnum? gapFillSource = null;
            if (_GapFill)
            {
                List<AmdGpuDeviceTelemetry> sysfsDevices = ReadSysfsDevices();
                if (GapFill(devices, sysfsDevices))
                {
                    gapFillSource = GpuTelemetrySourceEnum.AmdSysfs;
                }
            }

            return Build(GpuTelemetrySourceEnum.AmdSmiLibrary, gapFillSource, _Native.LibraryVersion, _Native.LibraryPath ?? String.Empty, devices);
        }

        private AmdGpuTelemetry? ReadSysfs()
        {
            return Build(GpuTelemetrySourceEnum.AmdSysfs, null, null, _Sysfs.Root, ReadSysfsDevices());
        }

        private List<AmdGpuDeviceTelemetry> ReadSysfsDevices()
        {
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceAmdSysfs, "read", null, System.Diagnostics.ActivityKind.Internal))
            {
                List<AmdGpuDeviceTelemetry> devices = _Sysfs.ReadDevices();
                call.SetTag("rigmonitor.gpu.device_count", devices.Count);
                call.Succeed();
                return devices;
            }
        }

        private AmdGpuTelemetry? ReadWindows()
        {
            if (_Windows == null)
            {
                return null;
            }

            List<AmdGpuDeviceTelemetry> devices = new List<AmdGpuDeviceTelemetry>();
            foreach (WindowsGpuAdapterReading reading in _Windows.GetAdapters().Where(adapter => adapter.VendorId == PciVendorIds.Amd))
            {
                AmdGpuDeviceTelemetry device = new AmdGpuDeviceTelemetry();
                bool apu = AmdSysfsBackend.IsApu(null, null, reading.Description, reading.DeviceId.ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                WindowsAdapterMapper.ApplyCommon(reading, device, apu);
                devices.Add(device);
            }

            return Build(GpuTelemetrySourceEnum.WindowsGraphicsKernel, null, null, "dxgi", devices);
        }

        private static AmdGpuTelemetry? Build(
            GpuTelemetrySourceEnum source,
            GpuTelemetrySourceEnum? gapFillSource,
            string? libraryVersion,
            string endpoint,
            List<AmdGpuDeviceTelemetry> devices)
        {
            if (devices.Count < 1)
            {
                return null;
            }

            ApplyApuMemory(devices);
            return new AmdGpuTelemetry
            {
                Source = source,
                GapFillSource = gapFillSource,
                LibraryVersion = libraryVersion,
                Endpoint = endpoint,
                Devices = devices
                    .OrderBy(device => device.DeviceIndex)
                    .ThenBy(device => device.PartitionId ?? String.Empty, StringComparer.Ordinal)
                    .ToList()
            };
        }
    }
}
