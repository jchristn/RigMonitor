namespace RigMonitor.Telemetry.Models.Ollama
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama model inventory DTO.
    /// </summary>
    internal class OllamaModelResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        [JsonPropertyName("modified_at")]
        public DateTime? ModifiedAt { get; set; } = null;

        [JsonPropertyName("size")]
        public long Size { get; set; } = 0L;

        [JsonPropertyName("digest")]
        public string? Digest { get; set; } = null;

        [JsonPropertyName("details")]
        public OllamaDetailsResponse? Details { get; set; } = null;
    }
}
