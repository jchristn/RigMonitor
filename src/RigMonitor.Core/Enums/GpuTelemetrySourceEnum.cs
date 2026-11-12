namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// Source that produced GPU telemetry.
    /// </summary>
    public enum GpuTelemetrySourceEnum
    {
        /// <summary>
        /// Unknown source.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// NVIDIA DCGM exporter (Prometheus text over HTTP).
        /// </summary>
        DcgmExporter = 1,
        /// <summary>
        /// ROCm AMD SMI native library (libamd_smi).
        /// </summary>
        AmdSmiLibrary = 2,
        /// <summary>
        /// Linux amdgpu sysfs.
        /// </summary>
        AmdSysfs = 3,
        /// <summary>
        /// AMD Device Metrics Exporter (Prometheus text over HTTP).
        /// </summary>
        AmdMetricsExporter = 4,
        /// <summary>
        /// Native macOS frameworks (IOKit, Metal, IOReport, IOHID).
        /// </summary>
        AppleNative = 5,
        /// <summary>
        /// Windows graphics kernel interfaces (DXGI, PDH, D3DKMT).
        /// </summary>
        WindowsGraphicsKernel = 6,
        /// <summary>
        /// Linux upstream drm/msm driver (devfreq, fdinfo, thermal).
        /// </summary>
        DrmMsm = 7,
        /// <summary>
        /// Linux downstream Qualcomm kgsl driver.
        /// </summary>
        Kgsl = 8
    }
}
