namespace RigMonitor.Telemetry.Gpu
{
    using System;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Amd;
    using RigMonitor.Telemetry.Gpu.Amd.Native;
    using RigMonitor.Telemetry.Gpu.Apple;
    using RigMonitor.Telemetry.Gpu.Nvidia;
    using RigMonitor.Telemetry.Gpu.Qualcomm;
    using RigMonitor.Telemetry.Gpu.Windows;
    using RigMonitor.Telemetry.Platform.Shared;
    using RigMonitor.Telemetry.Platform.Windows.Native;

    /// <summary>
    /// The four GPU vendor providers for this host, built for its platform.
    /// </summary>
    public sealed class GpuProviderSet : IDisposable
    {
        private readonly IDisposable?[] _Owned;

        /// <summary>
        /// Instantiate a provider set.
        /// </summary>
        /// <param name="nvidia">NVIDIA provider.</param>
        /// <param name="amd">AMD provider.</param>
        /// <param name="apple">Apple provider.</param>
        /// <param name="qualcomm">Qualcomm provider.</param>
        /// <param name="owned">Resources disposed with the set.</param>
        public GpuProviderSet(
            INvidiaGpuTelemetryProvider nvidia,
            IAmdGpuTelemetryProvider amd,
            IAppleGpuTelemetryProvider apple,
            IQualcommGpuTelemetryProvider qualcomm,
            params IDisposable?[] owned)
        {
            Nvidia = nvidia ?? throw new ArgumentNullException(nameof(nvidia));
            Amd = amd ?? throw new ArgumentNullException(nameof(amd));
            Apple = apple ?? throw new ArgumentNullException(nameof(apple));
            Qualcomm = qualcomm ?? throw new ArgumentNullException(nameof(qualcomm));
            _Owned = owned ?? Array.Empty<IDisposable?>();
        }

        /// <summary>
        /// NVIDIA provider.
        /// </summary>
        public INvidiaGpuTelemetryProvider Nvidia { get; }

        /// <summary>
        /// AMD provider.
        /// </summary>
        public IAmdGpuTelemetryProvider Amd { get; }

        /// <summary>
        /// Apple provider.
        /// </summary>
        public IAppleGpuTelemetryProvider Apple { get; }

        /// <summary>
        /// Qualcomm provider.
        /// </summary>
        public IQualcommGpuTelemetryProvider Qualcomm { get; }

        /// <summary>
        /// Build providers for the current host.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="dcgmClient">DCGM exporter client.</param>
        /// <returns>Provider set.</returns>
        public static GpuProviderSet Create(TelemetrySettings settings, IDcgmExporterClient dcgmClient)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (dcgmClient == null) throw new ArgumentNullException(nameof(dcgmClient));

            HostPlatformEnum platform = PlatformHelpers.GetHostPlatform();
            WindowsGraphicsKernelBackend? windows = null;
            if (OperatingSystem.IsWindows())
            {
                windows = new WindowsGraphicsKernelBackend(new WindowsGpuNative(), settings.WindowsGpuSamplerIntervalMs);
            }

            IAmdSmiNative? amdNative = null;
            if (OperatingSystem.IsLinux())
            {
                amdNative = new AmdSmiNative(settings.AmdSmiLibraryPath);
            }

            NvidiaDcgmGpuTelemetryProvider nvidia = new NvidiaDcgmGpuTelemetryProvider(settings, dcgmClient);
            AmdGpuTelemetryProvider amd = new AmdGpuTelemetryProvider(settings, platform, amdNative, null, null, windows);
            AppleGpuTelemetryProvider apple = new AppleGpuTelemetryProvider(settings, platform);
            QualcommGpuTelemetryProvider qualcomm = new QualcommGpuTelemetryProvider(settings, platform, windows);
            return new GpuProviderSet(nvidia, amd, apple, qualcomm, amd, apple, windows);
        }

        /// <summary>
        /// Dispose owned resources.
        /// </summary>
        public void Dispose()
        {
            foreach (IDisposable? owned in _Owned)
            {
                try
                {
                    owned?.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
