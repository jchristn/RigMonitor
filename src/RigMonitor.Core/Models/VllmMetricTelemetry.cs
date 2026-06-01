namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Single vLLM Prometheus metric sample.
    /// </summary>
    public class VllmMetricTelemetry
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Metric labels.
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Metric value.
        /// </summary>
        public double Value { get; set; } = 0D;
    }
}
