namespace RigMonitor.Telemetry.Models.Ollama
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama running models response DTO.
    /// </summary>
    internal class OllamaRunningResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaLoadedModelResponse> Models { get; set; } = new List<OllamaLoadedModelResponse>();
    }
}
