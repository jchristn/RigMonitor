namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Ollama runtime telemetry.
    /// </summary>
    public class OllamaTelemetry
    {
        /// <summary>
        /// Whether Ollama is currently available.
        /// </summary>
        public bool Available { get; set; } = false;

        /// <summary>
        /// Ollama base URL.
        /// </summary>
        public string BaseUrl { get; set; } = String.Empty;

        /// <summary>
        /// Ollama version string when available.
        /// </summary>
        public string Version { get; set; } = String.Empty;

        /// <summary>
        /// Time the Ollama data was collected.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total model count available to Ollama.
        /// </summary>
        public int AvailableModelCount
        {
            get
            {
                return _AvailableModelCount;
            }
            set
            {
                _AvailableModelCount = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Loaded model count.
        /// </summary>
        public int LoadedModelCount
        {
            get
            {
                return _LoadedModelCount;
            }
            set
            {
                _LoadedModelCount = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Models available on disk.
        /// </summary>
        public List<OllamaModelTelemetry> AvailableModels { get; set; } = new List<OllamaModelTelemetry>();

        /// <summary>
        /// Models currently loaded into memory.
        /// </summary>
        public List<OllamaLoadedModelTelemetry> LoadedModels { get; set; } = new List<OllamaLoadedModelTelemetry>();

        private int _AvailableModelCount = 0;
        private int _LoadedModelCount = 0;
    }
}
