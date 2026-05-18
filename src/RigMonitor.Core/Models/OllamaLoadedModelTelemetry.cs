namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Loaded Ollama model details.
    /// </summary>
    public class OllamaLoadedModelTelemetry
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
        /// Expiration time when provided by Ollama.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; } = null;

        /// <summary>
        /// Loaded model size in bytes.
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
        /// Estimated VRAM size in bytes.
        /// </summary>
        public long SizeVramBytes
        {
            get
            {
                return _SizeVramBytes;
            }
            set
            {
                _SizeVramBytes = Math.Max(0L, value);
            }
        }

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
        private long _SizeVramBytes = 0L;
    }
}
