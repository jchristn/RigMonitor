namespace RigMonitor.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;

    /// <summary>
    /// GPU vendor telemetry provider contract.
    /// </summary>
    /// <typeparam name="TTelemetry">Vendor section type.</typeparam>
    public interface IGpuVendorTelemetryProvider<TTelemetry> where TTelemetry : class
    {
        /// <summary>
        /// Vendor served by this provider.
        /// </summary>
        GpuVendorEnum Vendor { get; }

        /// <summary>
        /// Detect whether the vendor is reachable on this host and resolve the source to use.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Probe result.</returns>
        Task<GpuProbeResult> ProbeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Capture the vendor section.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Vendor section, or null when no current sample is available.</returns>
        Task<TTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// NVIDIA GPU telemetry provider.
    /// </summary>
    public interface INvidiaGpuTelemetryProvider : IGpuVendorTelemetryProvider<NvidiaGpuTelemetry>
    {
    }

    /// <summary>
    /// AMD GPU telemetry provider.
    /// </summary>
    public interface IAmdGpuTelemetryProvider : IGpuVendorTelemetryProvider<AmdGpuTelemetry>
    {
    }

    /// <summary>
    /// Apple GPU telemetry provider.
    /// </summary>
    public interface IAppleGpuTelemetryProvider : IGpuVendorTelemetryProvider<AppleGpuTelemetry>
    {
    }

    /// <summary>
    /// Qualcomm GPU telemetry provider.
    /// </summary>
    public interface IQualcommGpuTelemetryProvider : IGpuVendorTelemetryProvider<QualcommGpuTelemetry>
    {
    }
}
