namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Lightweight persisted telemetry sample record.
    /// </summary>
    public class TelemetrySampleRecord
    {
        /// <summary>
        /// Sample identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Configured persistence hostname.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Snapshot collection time.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Persistence write time.
        /// </summary>
        public DateTime PersistedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Host platform.
        /// </summary>
        public HostPlatformEnum HostPlatform { get; set; } = HostPlatformEnum.Unknown;

        /// <summary>
        /// Whether NVIDIA telemetry was available.
        /// </summary>
        public bool NvidiaAvailable { get; set; } = false;

        /// <summary>
        /// Whether Ollama telemetry was available.
        /// </summary>
        public bool OllamaAvailable { get; set; } = false;

        /// <summary>
        /// Whether vLLM telemetry was available.
        /// </summary>
        public bool VllmAvailable { get; set; } = false;

        /// <summary>
        /// Whether Utilyze telemetry was available.
        /// </summary>
        public bool UtilyzeAvailable { get; set; } = false;

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? CpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Logical CPU core count.
        /// </summary>
        public int? LogicalCoreCount { get; set; } = null;

        /// <summary>
        /// Total memory in bytes.
        /// </summary>
        public long? MemoryTotalBytes { get; set; } = null;

        /// <summary>
        /// Used memory in bytes.
        /// </summary>
        public long? MemoryUsedBytes { get; set; } = null;

        /// <summary>
        /// Available memory in bytes.
        /// </summary>
        public long? MemoryAvailableBytes { get; set; } = null;

        /// <summary>
        /// Memory utilization percentage.
        /// </summary>
        public double? MemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Aggregate network receive bytes per second.
        /// </summary>
        public double? NetworkReceiveBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Aggregate network transmit bytes per second.
        /// </summary>
        public double? NetworkTransmitBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk read operations per second.
        /// </summary>
        public double? DiskReadOperationsPerSecond { get; set; } = null;

        /// <summary>
        /// Disk write operations per second.
        /// </summary>
        public double? DiskWriteOperationsPerSecond { get; set; } = null;

        /// <summary>
        /// Disk read queue depth.
        /// </summary>
        public double? DiskReadQueueDepth { get; set; } = null;

        /// <summary>
        /// Disk write queue depth.
        /// </summary>
        public double? DiskWriteQueueDepth { get; set; } = null;

        /// <summary>
        /// GPU device count.
        /// </summary>
        public int? GpuDeviceCount { get; set; } = null;

        /// <summary>
        /// Average GPU utilization percentage across devices.
        /// </summary>
        public double? GpuAverageUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average GPU memory utilization percentage across devices.
        /// </summary>
        public double? GpuAverageMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average GPU temperature in Celsius across devices.
        /// </summary>
        public double? GpuAverageTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Total GPU power usage in watts.
        /// </summary>
        public double? GpuTotalPowerUsageWatts { get; set; } = null;

        /// <summary>
        /// Ollama available model count.
        /// </summary>
        public int? OllamaAvailableModelCount { get; set; } = null;

        /// <summary>
        /// Ollama loaded model count.
        /// </summary>
        public int? OllamaLoadedModelCount { get; set; } = null;

        /// <summary>
        /// vLLM running request count.
        /// </summary>
        public double? VllmRunningRequests { get; set; } = null;

        /// <summary>
        /// vLLM waiting request count.
        /// </summary>
        public double? VllmWaitingRequests { get; set; } = null;

        /// <summary>
        /// vLLM GPU cache usage percentage.
        /// </summary>
        public double? VllmGpuCacheUsagePercent { get; set; } = null;

        /// <summary>
        /// Utilyze device count.
        /// </summary>
        public int? UtilyzeDeviceCount { get; set; } = null;
    }
}
