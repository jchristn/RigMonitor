namespace RigMonitor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Parses telemetry section selection flags from the request query string.
    /// </summary>
    public static class TelemetryRequestParser
    {
        private static readonly HashSet<string> _RecognizedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system",
            "cpu",
            "memory",
            "network",
            "disk",
            "gpu",
            "ollama"
        };

        /// <summary>
        /// Parse telemetry request options from a raw path and query string.
        /// </summary>
        /// <param name="rawWithQuery">Raw URL path including the query string.</param>
        /// <returns>Telemetry request options.</returns>
        public static TelemetryRequestOptions Parse(string? rawWithQuery)
        {
            if (String.IsNullOrWhiteSpace(rawWithQuery))
            {
                return TelemetryRequestOptions.All();
            }

            int queryIndex = rawWithQuery.IndexOf('?');
            if (queryIndex < 0 || queryIndex >= rawWithQuery.Length - 1)
            {
                return TelemetryRequestOptions.All();
            }

            string query = rawWithQuery.Substring(queryIndex + 1);
            string[] segments = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Dictionary<string, bool> selectors = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            bool hasRecognizedKey = false;

            foreach (string segment in segments)
            {
                string[] pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                string key = Uri.UnescapeDataString(pair[0]);
                if (!_RecognizedKeys.Contains(key))
                {
                    continue;
                }

                hasRecognizedKey = true;

                string? value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : null;
                selectors[key] = !String.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }

            if (!hasRecognizedKey)
            {
                return TelemetryRequestOptions.All();
            }

            TelemetryRequestOptions options = TelemetryRequestOptions.None();
            options.IncludeSystem = ReadValue(selectors, "system");
            options.IncludeCpu = ReadValue(selectors, "cpu");
            options.IncludeMemory = ReadValue(selectors, "memory");
            options.IncludeNetwork = ReadValue(selectors, "network");
            options.IncludeDisk = ReadValue(selectors, "disk");
            options.IncludeGpu = ReadValue(selectors, "gpu");
            options.IncludeOllama = ReadValue(selectors, "ollama");
            return options;
        }

        private static bool ReadValue(Dictionary<string, bool> selectors, string key)
        {
            if (selectors.TryGetValue(key, out bool value))
            {
                return value;
            }

            return false;
        }
    }
}
