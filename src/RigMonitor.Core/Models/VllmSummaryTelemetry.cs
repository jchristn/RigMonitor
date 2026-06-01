namespace RigMonitor.Core.Models
{
    /// <summary>
    /// Normalized vLLM summary values derived from Prometheus metrics when present.
    /// </summary>
    public class VllmSummaryTelemetry
    {
        /// <summary>
        /// Number of currently running requests.
        /// </summary>
        public double? RunningRequests { get; set; } = null;

        /// <summary>
        /// Number of waiting or queued requests.
        /// </summary>
        public double? WaitingRequests { get; set; } = null;

        /// <summary>
        /// Number of swapped requests.
        /// </summary>
        public double? SwappedRequests { get; set; } = null;

        /// <summary>
        /// GPU KV cache usage percentage.
        /// </summary>
        public double? GpuCacheUsagePercent { get; set; } = null;

        /// <summary>
        /// CPU KV cache usage percentage.
        /// </summary>
        public double? CpuCacheUsagePercent { get; set; } = null;

        /// <summary>
        /// Total prompt tokens processed.
        /// </summary>
        public double? PromptTokensTotal { get; set; } = null;

        /// <summary>
        /// Total generation tokens processed.
        /// </summary>
        public double? GenerationTokensTotal { get; set; } = null;

        /// <summary>
        /// Total successful requests.
        /// </summary>
        public double? SuccessfulRequestsTotal { get; set; } = null;
    }
}
