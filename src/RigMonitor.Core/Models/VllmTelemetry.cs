namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// vLLM Prometheus telemetry payload.
    /// </summary>
    public class VllmTelemetry
    {
        /// <summary>
        /// Whether vLLM telemetry is currently available.
        /// </summary>
        public bool Available { get; set; } = false;

        /// <summary>
        /// Metrics endpoint used for collection.
        /// </summary>
        public string MetricsEndpoint { get; set; } = String.Empty;

        /// <summary>
        /// Collection timestamp.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Model names discovered from vLLM metric labels.
        /// </summary>
        public List<string> ModelNames { get; set; } = new List<string>();

        /// <summary>
        /// Normalized summary values.
        /// </summary>
        public VllmSummaryTelemetry Summary { get; set; } = new VllmSummaryTelemetry();

        /// <summary>
        /// Raw vLLM Prometheus samples.
        /// </summary>
        public List<VllmMetricTelemetry> Metrics { get; set; } = new List<VllmMetricTelemetry>();
    }
}
