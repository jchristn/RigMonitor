namespace RigMonitor.Telemetry.Models.Ollama
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama running model DTO.
    /// </summary>
    internal class OllamaLoadedModelResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        [JsonPropertyName("digest")]
        public string? Digest { get; set; } = null;

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; } = null;

        [JsonPropertyName("size")]
        public long Size { get; set; } = 0L;

        [JsonPropertyName("size_vram")]
        public long SizeVram { get; set; } = 0L;

        [JsonPropertyName("details")]
        public OllamaDetailsResponse? Details { get; set; } = null;
    }
}
