namespace RigMonitor.Core.Models
{
    /// <summary>
    /// Controls which telemetry sections are collected.
    /// </summary>
    public class TelemetryRequestOptions
    {
        /// <summary>
        /// Whether to include the system section.
        /// </summary>
        public bool IncludeSystem { get; set; } = true;

        /// <summary>
        /// Whether to include the CPU section.
        /// </summary>
        public bool IncludeCpu { get; set; } = true;

        /// <summary>
        /// Whether to include the memory section.
        /// </summary>
        public bool IncludeMemory { get; set; } = true;

        /// <summary>
        /// Whether to include the network section.
        /// </summary>
        public bool IncludeNetwork { get; set; } = true;

        /// <summary>
        /// Whether to include the disk section.
        /// </summary>
        public bool IncludeDisk { get; set; } = true;

        /// <summary>
        /// Whether to include the GPU section.
        /// </summary>
        public bool IncludeGpu { get; set; } = true;

        /// <summary>
        /// Whether to include the Ollama section.
        /// </summary>
        public bool IncludeOllama { get; set; } = true;

        /// <summary>
        /// Create an option set with every section enabled.
        /// </summary>
        /// <returns>Telemetry request options.</returns>
        public static TelemetryRequestOptions All()
        {
            return new TelemetryRequestOptions();
        }

        /// <summary>
        /// Create an option set with every section disabled.
        /// </summary>
        /// <returns>Telemetry request options.</returns>
        public static TelemetryRequestOptions None()
        {
            return new TelemetryRequestOptions
            {
                IncludeSystem = false,
                IncludeCpu = false,
                IncludeMemory = false,
                IncludeNetwork = false,
                IncludeDisk = false,
                IncludeGpu = false,
                IncludeOllama = false
            };
        }
    }
}
