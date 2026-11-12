namespace RigMonitor.Telemetry.Gpu.Prometheus
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// A single parsed Prometheus sample.
    /// </summary>
    public sealed class PrometheusSample
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Labels (case-insensitive keys).
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sample value.
        /// </summary>
        public double Value { get; set; } = 0D;

        /// <summary>
        /// Read the first non-empty label among candidates.
        /// </summary>
        /// <param name="candidates">Label names.</param>
        /// <returns>Label value or empty string.</returns>
        public string Label(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (Labels.TryGetValue(candidate, out string? value) && !String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return String.Empty;
        }
    }

    /// <summary>
    /// Parser for the Prometheus text exposition format.
    /// </summary>
    public static class PrometheusTextParser
    {
        /// <summary>
        /// Parse Prometheus text into samples. Comments, blank lines, and unparseable lines are skipped.
        /// </summary>
        /// <param name="text">Exposition text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Samples.</returns>
        public static List<PrometheusSample> Parse(string? text, CancellationToken cancellationToken = default)
        {
            List<PrometheusSample> samples = new List<PrometheusSample>();
            if (String.IsNullOrWhiteSpace(text))
            {
                return samples;
            }

            string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PrometheusSample? sample = ParseLine(line);
                if (sample != null)
                {
                    samples.Add(sample);
                }
            }

            return samples;
        }

        /// <summary>
        /// Parse one exposition line.
        /// </summary>
        /// <param name="line">Line.</param>
        /// <returns>Sample, or null for comments and malformed lines.</returns>
        public static PrometheusSample? ParseLine(string line)
        {
            if (String.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            line = line.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                return null;
            }

            string metricName;
            string remainder;
            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int braceStart = line.IndexOf('{');
            int firstSpace = line.IndexOf(' ');
            if (braceStart >= 0 && (firstSpace < 0 || braceStart < firstSpace))
            {
                int braceEnd = FindClosingBrace(line, braceStart);
                if (braceEnd < 0)
                {
                    return null;
                }

                metricName = line.Substring(0, braceStart).Trim();
                labels = ParseLabels(line.Substring(braceStart + 1, braceEnd - braceStart - 1));
                remainder = line.Substring(braceEnd + 1).Trim();
            }
            else
            {
                if (firstSpace < 0)
                {
                    return null;
                }

                metricName = line.Substring(0, firstSpace).Trim();
                remainder = line.Substring(firstSpace + 1).Trim();
            }

            if (String.IsNullOrWhiteSpace(metricName) || String.IsNullOrWhiteSpace(remainder))
            {
                return null;
            }

            string valueText = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (!TryParseValue(valueText, out double value))
            {
                return null;
            }

            return new PrometheusSample
            {
                Name = metricName,
                Labels = labels,
                Value = value
            };
        }

        private static bool TryParseValue(string text, out double value)
        {
            if (String.Equals(text, "NaN", StringComparison.OrdinalIgnoreCase))
            {
                value = Double.NaN;
                return true;
            }

            if (String.Equals(text, "+Inf", StringComparison.OrdinalIgnoreCase) || String.Equals(text, "Inf", StringComparison.OrdinalIgnoreCase))
            {
                value = Double.PositiveInfinity;
                return true;
            }

            if (String.Equals(text, "-Inf", StringComparison.OrdinalIgnoreCase))
            {
                value = Double.NegativeInfinity;
                return true;
            }

            return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static int FindClosingBrace(string line, int braceStart)
        {
            bool inQuotes = false;
            for (int i = braceStart + 1; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && inQuotes)
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == '}' && !inQuotes)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Dictionary<string, string> ParseLabels(string input)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;

            while (i < input.Length)
            {
                while (i < input.Length && (input[i] == ',' || Char.IsWhiteSpace(input[i]))) i++;
                int keyStart = i;
                while (i < input.Length && input[i] != '=') i++;
                if (i >= input.Length) break;

                string key = input.Substring(keyStart, i - keyStart).Trim();
                i++;
                while (i < input.Length && Char.IsWhiteSpace(input[i])) i++;

                System.Text.StringBuilder value = new System.Text.StringBuilder();
                if (i < input.Length && input[i] == '"')
                {
                    i++;
                    while (i < input.Length && input[i] != '"')
                    {
                        if (input[i] == '\\' && i + 1 < input.Length)
                        {
                            char next = input[i + 1];
                            value.Append(next == 'n' ? '\n' : next);
                            i += 2;
                            continue;
                        }

                        value.Append(input[i]);
                        i++;
                    }

                    i++;
                }
                else
                {
                    while (i < input.Length && input[i] != ',')
                    {
                        value.Append(input[i]);
                        i++;
                    }
                }

                if (!String.IsNullOrWhiteSpace(key))
                {
                    labels[key] = value.ToString().Trim();
                }
            }

            return labels;
        }
    }
}
