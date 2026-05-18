namespace RigMonitor.Server.Serialization
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Shared JSON serializer settings.
    /// </summary>
    public static class RigMonitorJsonSerializer
    {
        /// <summary>
        /// Default serializer options.
        /// </summary>
        public static JsonSerializerOptions Options { get; } = BuildOptions(false);

        /// <summary>
        /// Indented serializer options.
        /// </summary>
        public static JsonSerializerOptions IndentedOptions { get; } = BuildOptions(true);

        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="value">Value to serialize.</param>
        /// <param name="writeIndented">Whether to pretty print JSON.</param>
        /// <returns>Serialized JSON.</returns>
        public static string Serialize(object value, bool writeIndented = false)
        {
            return JsonSerializer.Serialize(value, writeIndented ? IndentedOptions : Options);
        }

        /// <summary>
        /// Deserialize a JSON document.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON text.</param>
        /// <returns>Deserialized instance.</returns>
        public static T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        private static JsonSerializerOptions BuildOptions(bool writeIndented)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = writeIndented
            };

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}
