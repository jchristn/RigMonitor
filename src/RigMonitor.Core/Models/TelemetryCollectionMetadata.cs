namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Collection metadata for the telemetry snapshot.
    /// </summary>
    public class TelemetryCollectionMetadata
    {
        /// <summary>
        /// Time the collection metadata was produced.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stale threshold in milliseconds applied to section freshness.
        /// </summary>
        public int StaleAfterMs
        {
            get
            {
                return _StaleAfterMs;
            }
            set
            {
                _StaleAfterMs = value < 0 ? 0 : value;
            }
        }

        /// <summary>
        /// System section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus System { get; set; } = new TelemetrySectionCollectionStatus();

        /// <summary>
        /// CPU section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus Cpu { get; set; } = new TelemetrySectionCollectionStatus();

        /// <summary>
        /// Memory section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus Memory { get; set; } = new TelemetrySectionCollectionStatus();

        /// <summary>
        /// Network section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus Network { get; set; } = new TelemetrySectionCollectionStatus();

        /// <summary>
        /// Disk section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus Disk { get; set; } = new TelemetrySectionCollectionStatus();

        /// <summary>
        /// GPU section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus Gpu { get; set; } = new TelemetrySectionCollectionStatus();

        /// <summary>
        /// Ollama section metadata.
        /// </summary>
        public TelemetrySectionCollectionStatus Ollama { get; set; } = new TelemetrySectionCollectionStatus();

        private int _StaleAfterMs = 0;
    }
}
