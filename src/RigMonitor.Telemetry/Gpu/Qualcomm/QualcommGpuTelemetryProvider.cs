namespace RigMonitor.Telemetry.Gpu.Qualcomm
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Windows;

    /// <summary>
    /// Qualcomm Adreno GPU telemetry provider. Windows on Snapdragon uses the Windows graphics kernel backend; Linux uses
    /// upstream drm/msm (devfreq, fdinfo, thermal) or the downstream kgsl driver.
    /// </summary>
    public sealed class QualcommGpuTelemetryProvider : IQualcommGpuTelemetryProvider
    {
        private readonly TelemetrySettings _Settings;
        private readonly HostPlatformEnum _Platform;
        private readonly IWindowsGpuAdapterSource? _Windows;
        private readonly DrmMsmBackend _DrmMsm;
        private readonly KgslBackend _Kgsl;
        private readonly TimeProvider _TimeProvider;
        private GpuTelemetrySourceEnum? _ResolvedSource = null;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="platform">Host platform.</param>
        /// <param name="windows">Windows adapter source.</param>
        /// <param name="drmMsm">drm/msm backend.</param>
        /// <param name="kgsl">kgsl backend.</param>
        /// <param name="timeProvider">Time provider.</param>
        public QualcommGpuTelemetryProvider(
            TelemetrySettings settings,
            HostPlatformEnum platform,
            IWindowsGpuAdapterSource? windows = null,
            DrmMsmBackend? drmMsm = null,
            KgslBackend? kgsl = null,
            TimeProvider? timeProvider = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Platform = platform;
            _Windows = windows;
            _DrmMsm = drmMsm ?? new DrmMsmBackend(settings.QualcommSysfsRoot, settings.QualcommProcRoot);
            _Kgsl = kgsl ?? new KgslBackend(settings.QualcommSysfsRoot);
            _TimeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Vendor served by this provider.
        /// </summary>
        public GpuVendorEnum Vendor
        {
            get
            {
                return GpuVendorEnum.Qualcomm;
            }
        }

        /// <summary>
        /// Resolve the Qualcomm source for this host.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Probe result.</returns>
        public Task<GpuProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            _ResolvedSource = null;
            if (!_Settings.QualcommEnabled)
            {
                return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Qualcomm, "Qualcomm telemetry is disabled in settings."));
            }

            string mode = _Settings.QualcommSource;
            if (_Platform == HostPlatformEnum.Windows && (mode == "auto" || mode == "windows"))
            {
                if (_Windows != null && _Windows.IsAvailable())
                {
                    WindowsGpuAdapterReading? adapter = _Windows.GetAdapters().FirstOrDefault(IsQualcomm);
                    if (adapter != null)
                    {
                        _ResolvedSource = GpuTelemetrySourceEnum.WindowsGraphicsKernel;
                        return Task.FromResult(Available(GpuTelemetrySourceEnum.WindowsGraphicsKernel, "dxgi", adapter.Description));
                    }
                }

                return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Qualcomm, "No Qualcomm adapters were enumerated by DXGI."));
            }

            if (_Platform == HostPlatformEnum.Linux)
            {
                using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceQualcommLinux, "probe", null, System.Diagnostics.ActivityKind.Internal))
                {
                    if ((mode == "auto" || mode == "drmMsm") && _DrmMsm.HasDevices())
                    {
                        _DrmMsm.ReadDevices(_TimeProvider.GetUtcNow().UtcDateTime);
                        _ResolvedSource = GpuTelemetrySourceEnum.DrmMsm;
                        RigMonitorInstrumentation.SetGpuNativeFeature("drmMsm", true);
                        call.Succeed();
                        return Task.FromResult(Available(GpuTelemetrySourceEnum.DrmMsm, _DrmMsm.Root, null));
                    }

                    if ((mode == "auto" || mode == "kgsl") && _Kgsl.HasDevices())
                    {
                        _ResolvedSource = GpuTelemetrySourceEnum.Kgsl;
                        RigMonitorInstrumentation.SetGpuNativeFeature("kgsl", true);
                        call.Succeed();
                        return Task.FromResult(Available(GpuTelemetrySourceEnum.Kgsl, _Kgsl.DevicePath, null));
                    }

                    call.Fail(RigMonitorTelemetryNames.OutcomeUnavailable, "No Adreno GPU found.");
                }

                return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Qualcomm, "No Adreno devfreq or kgsl device was found."));
            }

            return Task.FromResult(GpuProbeResult.Unavailable(GpuVendorEnum.Qualcomm, "Qualcomm telemetry is supported on Windows and Linux."));
        }

        /// <summary>
        /// Capture the Qualcomm section.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Qualcomm section, or null when no current sample is available.</returns>
        public async Task<QualcommGpuTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            switch (_ResolvedSource)
            {
                case GpuTelemetrySourceEnum.WindowsGraphicsKernel:
                    return ReadWindows();
                case GpuTelemetrySourceEnum.DrmMsm:
                    return await Task.Run(() => Build(GpuTelemetrySourceEnum.DrmMsm, _DrmMsm.Root, Read(() => _DrmMsm.ReadDevices(_TimeProvider.GetUtcNow().UtcDateTime))), cancellationToken).ConfigureAwait(false);
                case GpuTelemetrySourceEnum.Kgsl:
                    return await Task.Run(() => Build(GpuTelemetrySourceEnum.Kgsl, _Kgsl.DevicePath, Read(() => _Kgsl.ReadDevices())), cancellationToken).ConfigureAwait(false);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Map a Windows adapter reading to a Qualcomm device.
        /// </summary>
        /// <param name="reading">Reading.</param>
        /// <returns>Device.</returns>
        public static QualcommGpuDeviceTelemetry MapWindowsAdapter(WindowsGpuAdapterReading reading)
        {
            QualcommGpuDeviceTelemetry device = new QualcommGpuDeviceTelemetry();
            WindowsAdapterMapper.ApplyCommon(reading, device, true);
            device.AdapterLuid = reading.Luid;
            device.DxgiDeviceId = "0x" + reading.DeviceId.ToString("x4", CultureInfo.InvariantCulture);
            device.UtilizationScope = reading.EngineUtilization.Count > 0 ? GpuUtilizationScopeEnum.Device : null;
            device.EngineUtilization = new Dictionary<string, double>(reading.EngineUtilization, StringComparer.Ordinal);
            device.SharedMemoryUsedMegabytes = reading.NonLocalUsageBytes.HasValue ? GpuMetricMath.BytesToMegabytes(reading.NonLocalUsageBytes.Value) : null;
            device.SharedMemoryBudgetMegabytes = reading.NonLocalBudgetBytes.HasValue ? GpuMetricMath.BytesToMegabytes(reading.NonLocalBudgetBytes.Value) : null;
            device.DedicatedMemoryUsedMegabytes = reading.LocalUsageBytes.HasValue ? GpuMetricMath.BytesToMegabytes(reading.LocalUsageBytes.Value) : null;
            device.DedicatedMemoryBudgetMegabytes = reading.LocalBudgetBytes.HasValue ? GpuMetricMath.BytesToMegabytes(reading.LocalBudgetBytes.Value) : null;
            device.CurrentFrequencyMHz = reading.CurrentFrequencyMHz;
            device.MaxFrequencyMHz = reading.MaxFrequencyMHz;
            device.MaxTemperatureCelsius = reading.TemperatureCelsius;

            const GpuTelemetrySourceEnum source = GpuTelemetrySourceEnum.WindowsGraphicsKernel;
            if (reading.EngineUtilization.Count > 0) device.FieldSources["utilization"] = source;
            if (device.Metrics.MemoryTotalMegabytes > 0D) device.FieldSources["memory"] = source;
            if (reading.CurrentFrequencyMHz.HasValue) device.FieldSources["frequency"] = source;
            if (reading.TemperatureCelsius.HasValue) device.FieldSources["temperature"] = source;
            return device;
        }

        private static bool IsQualcomm(WindowsGpuAdapterReading reading)
        {
            return reading.VendorId == PciVendorIds.Qualcomm
                || reading.VendorId == PciVendorIds.QualcommAlternate
                || reading.Description.IndexOf("Adreno", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private QualcommGpuTelemetry? ReadWindows()
        {
            if (_Windows == null)
            {
                return null;
            }

            List<QualcommGpuDeviceTelemetry> devices = _Windows.GetAdapters().Where(IsQualcomm).Select(MapWindowsAdapter).ToList();
            RigMonitorInstrumentation.SetGpuNativeFeature(
                "d3dkmtPerfData",
                devices.Any(device => device.CurrentFrequencyMHz.HasValue || device.MaxTemperatureCelsius.HasValue));
            return Build(GpuTelemetrySourceEnum.WindowsGraphicsKernel, "dxgi", devices);
        }

        private static List<QualcommGpuDeviceTelemetry> Read(Func<List<QualcommGpuDeviceTelemetry>> reader)
        {
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceQualcommLinux, "read", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    List<QualcommGpuDeviceTelemetry> devices = reader();
                    call.Succeed();
                    return devices;
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    RigMonitorInstrumentation.RecordGpuNativeError("qualcomm", "drmMsm");
                    throw;
                }
            }
        }

        private static QualcommGpuTelemetry? Build(GpuTelemetrySourceEnum source, string endpoint, List<QualcommGpuDeviceTelemetry> devices)
        {
            if (devices.Count < 1)
            {
                return null;
            }

            return new QualcommGpuTelemetry
            {
                Source = source,
                Endpoint = endpoint,
                Devices = devices.OrderBy(device => device.DeviceIndex).ToList()
            };
        }

        private static GpuProbeResult Available(GpuTelemetrySourceEnum source, string endpoint, string? detail)
        {
            return new GpuProbeResult
            {
                Vendor = GpuVendorEnum.Qualcomm,
                Available = true,
                Source = source,
                Endpoint = endpoint,
                Detail = detail
            };
        }
    }
}
