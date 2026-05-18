namespace RigMonitor.Telemetry.Models.Ollama
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama model details DTO.
    /// </summary>
    internal class OllamaDetailsResponse
    {
        [JsonPropertyName("family")]
        public string? Family { get; set; } = null;

        [JsonPropertyName("format")]
        public string? Format { get; set; } = null;

        [JsonPropertyName("parameter_size")]
        public string? ParameterSize { get; set; } = null;

        [JsonPropertyName("quantization_level")]
        public string? QuantizationLevel { get; set; } = null;
    }
}
