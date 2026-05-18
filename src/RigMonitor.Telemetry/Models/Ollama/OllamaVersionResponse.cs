namespace RigMonitor.Telemetry.Models.Ollama
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama version response DTO.
    /// </summary>
    internal class OllamaVersionResponse
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; } = null;
    }
}
