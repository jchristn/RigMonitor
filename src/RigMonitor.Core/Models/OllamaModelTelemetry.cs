namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Available Ollama model inventory entry.
    /// </summary>
    public class OllamaModelTelemetry
    {
        /// <summary>
        /// Friendly model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Canonical model identifier.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Model digest.
        /// </summary>
        public string Digest { get; set; } = String.Empty;

        /// <summary>
        /// Size on disk in bytes.
        /// </summary>
        public long SizeBytes
        {
            get
            {
                return _SizeBytes;
            }
            set
            {
                _SizeBytes = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Modification timestamp from Ollama when provided.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; } = null;

        /// <summary>
        /// Model family name.
        /// </summary>
        public string Family { get; set; } = String.Empty;

        /// <summary>
        /// Model format.
        /// </summary>
        public string Format { get; set; } = String.Empty;

        /// <summary>
        /// Parameter count description.
        /// </summary>
        public string ParameterSize { get; set; } = String.Empty;

        /// <summary>
        /// Quantization level.
        /// </summary>
        public string QuantizationLevel { get; set; } = String.Empty;

        private long _SizeBytes = 0L;
    }
}
