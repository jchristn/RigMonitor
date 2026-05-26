namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Full telemetry snapshot.
    /// </summary>
    public class TelemetrySnapshot
    {
        /// <summary>
        /// Snapshot collection time.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Host platform.
        /// </summary>
        public HostPlatformEnum HostPlatform { get; set; } = HostPlatformEnum.Unknown;

        /// <summary>
        /// Whether NVIDIA GPU telemetry is available.
        /// </summary>
        public bool NvidiaAvailable { get; set; } = false;

        /// <summary>
        /// Whether Ollama telemetry is available.
        /// </summary>
        public bool OllamaAvailable { get; set; } = false;

        /// <summary>
        /// System metadata.
        /// </summary>
        public SystemTelemetry? System { get; set; } = null;

        /// <summary>
        /// CPU telemetry.
        /// </summary>
        public CpuTelemetry? Cpu { get; set; } = null;

        /// <summary>
        /// Memory telemetry.
        /// </summary>
        public MemoryTelemetry? Memory { get; set; } = null;

        /// <summary>
        /// Network telemetry.
        /// </summary>
        public NetworkTelemetry? Network { get; set; } = null;

        /// <summary>
        /// Disk telemetry.
        /// </summary>
        public DiskTelemetry? Disk { get; set; } = null;

        /// <summary>
        /// GPU telemetry when available.
        /// </summary>
        public GpuTelemetry? Gpu { get; set; } = null;

        /// <summary>
        /// Ollama telemetry when available.
        /// </summary>
        public OllamaTelemetry? Ollama { get; set; } = null;

        /// <summary>
        /// Collection metadata for each telemetry section.
        /// </summary>
        public TelemetryCollectionMetadata? Collection { get; set; } = null;
    }
}
