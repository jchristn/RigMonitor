namespace RigMonitor.Telemetry.Models.Ollama
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama tags response DTO.
    /// </summary>
    internal class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelResponse> Models { get; set; } = new List<OllamaModelResponse>();
    }
}
