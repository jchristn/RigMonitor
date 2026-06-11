namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Filter criteria for telemetry history search.
    /// </summary>
    public class TelemetryHistorySearchFilter
    {
        /// <summary>
        /// Optional hostname filter.
        /// </summary>
        public string? Hostname { get; set; } = null;

        /// <summary>
        /// Optional host platform filter.
        /// </summary>
        public HostPlatformEnum? HostPlatform { get; set; } = null;

        /// <summary>
        /// Optional GPU UUID filter.
        /// </summary>
        public string? GpuUuid { get; set; } = null;

        /// <summary>
        /// Optional GPU model substring filter.
        /// </summary>
        public string? GpuModel { get; set; } = null;

        /// <summary>
        /// Optional NVIDIA availability filter.
        /// </summary>
        public bool? NvidiaAvailable { get; set; } = null;

        /// <summary>
        /// Optional Ollama availability filter.
        /// </summary>
        public bool? OllamaAvailable { get; set; } = null;

        /// <summary>
        /// Optional vLLM availability filter.
        /// </summary>
        public bool? VllmAvailable { get; set; } = null;

        /// <summary>
        /// Optional Utilyze availability filter.
        /// </summary>
        public bool? UtilyzeAvailable { get; set; } = null;

        /// <summary>
        /// Inclusive start time.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// Inclusive end time.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        /// <summary>
        /// Minimum CPU utilization percentage.
        /// </summary>
        public double? MinCpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Maximum CPU utilization percentage.
        /// </summary>
        public double? MaxCpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Minimum memory utilization percentage.
        /// </summary>
        public double? MinMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Maximum memory utilization percentage.
        /// </summary>
        public double? MaxMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Minimum average GPU utilization percentage.
        /// </summary>
        public double? MinGpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Maximum average GPU utilization percentage.
        /// </summary>
        public double? MaxGpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Minimum average GPU temperature in Celsius.
        /// </summary>
        public double? MinGpuTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Maximum average GPU temperature in Celsius.
        /// </summary>
        public double? MaxGpuTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// 1-based page number.
        /// </summary>
        public int Page
        {
            get
            {
                return _Page;
            }
            set
            {
                _Page = Math.Max(1, value);
            }
        }

        /// <summary>
        /// Page size. Minimum 1, maximum 250. Default is 25.
        /// </summary>
        public int PageSize
        {
            get
            {
                return _PageSize;
            }
            set
            {
                _PageSize = Math.Clamp(value, 1, 250);
            }
        }

        private int _Page = 1;
        private int _PageSize = 25;
    }
}
