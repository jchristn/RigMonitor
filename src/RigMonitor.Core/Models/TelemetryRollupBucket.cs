namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Bucketized telemetry aggregate values.
    /// </summary>
    public class TelemetryRollupBucket
    {
        /// <summary>
        /// Bucket start time in UTC.
        /// </summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket end time in UTC.
        /// </summary>
        public DateTime BucketEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sample count in this bucket.
        /// </summary>
        public long SampleCount { get; set; } = 0L;

        /// <summary>
        /// Average CPU utilization percentage.
        /// </summary>
        public double? AverageCpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average memory utilization percentage.
        /// </summary>
        public double? AverageMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average network receive bytes per second.
        /// </summary>
        public double? AverageNetworkReceiveBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Average network transmit bytes per second.
        /// </summary>
        public double? AverageNetworkTransmitBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Average disk read operations per second.
        /// </summary>
        public double? AverageDiskReadOperationsPerSecond { get; set; } = null;

        /// <summary>
        /// Average disk write operations per second.
        /// </summary>
        public double? AverageDiskWriteOperationsPerSecond { get; set; } = null;

        /// <summary>
        /// Average GPU utilization percentage.
        /// </summary>
        public double? AverageGpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Minimum GPU utilization percentage.
        /// </summary>
        public double? MinGpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Maximum GPU utilization percentage.
        /// </summary>
        public double? MaxGpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average GPU memory utilization percentage.
        /// </summary>
        public double? AverageGpuMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average GPU temperature in Celsius.
        /// </summary>
        public double? AverageGpuTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Average total GPU power usage in watts.
        /// </summary>
        public double? AverageGpuPowerUsageWatts { get; set; } = null;

        /// <summary>
        /// Average Ollama loaded model count.
        /// </summary>
        public double? AverageOllamaLoadedModelCount { get; set; } = null;

        /// <summary>
        /// Average vLLM running request count.
        /// </summary>
        public double? AverageVllmRunningRequests { get; set; } = null;

        /// <summary>
        /// Average vLLM waiting request count.
        /// </summary>
        public double? AverageVllmWaitingRequests { get; set; } = null;

        /// <summary>
        /// Average vLLM GPU cache usage percentage.
        /// </summary>
        public double? AverageVllmGpuCacheUsagePercent { get; set; } = null;

        /// <summary>
        /// Average Utilyze device count.
        /// </summary>
        public double? AverageUtilyzeDeviceCount { get; set; } = null;
    }
}
