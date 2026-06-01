namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// vLLM telemetry provider backed by the vLLM Prometheus metrics endpoint.
    /// </summary>
    public class VllmTelemetryProvider : IVllmTelemetryProvider
    {
        private readonly TelemetrySettings _Settings;
        private readonly HttpClient _HttpClient;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public VllmTelemetryProvider(TelemetrySettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(settings.RequestTimeoutMs)
            };
        }

        /// <summary>
        /// Determine whether vLLM metrics are reachable.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when enabled and reachable.</returns>
        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            if (!_Settings.VllmEnabled)
            {
                return false;
            }

            string? metrics = await TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            return !String.IsNullOrWhiteSpace(metrics)
                && metrics.Contains("vllm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Capture vLLM telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>vLLM telemetry when available.</returns>
        public async Task<VllmTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            if (!_Settings.VllmEnabled)
            {
                return null;
            }

            string? rawMetrics = await TryGetMetricsAsync(cancellationToken).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(rawMetrics))
            {
                return null;
            }

            List<VllmMetricTelemetry> metrics = ParseMetrics(rawMetrics, cancellationToken);
            if (metrics.Count < 1)
            {
                return null;
            }

            return new VllmTelemetry
            {
                Available = true,
                MetricsEndpoint = _Settings.VllmMetricsUrl,
                CollectedUtc = DateTime.UtcNow,
                ModelNames = ExtractModelNames(metrics),
                Summary = BuildSummary(metrics),
                Metrics = metrics
            };
        }

        private async Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _Settings.VllmMetricsUrl))
                using (HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<VllmMetricTelemetry> ParseMetrics(string rawMetrics, CancellationToken cancellationToken)
        {
            List<VllmMetricTelemetry> metrics = new List<VllmMetricTelemetry>();
            string[] lines = rawMetrics.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int valueSeparator = line.LastIndexOf(' ');
                if (valueSeparator < 0)
                {
                    continue;
                }

                string left = line.Substring(0, valueSeparator).Trim();
                string valueText = line.Substring(valueSeparator + 1).Trim();
                if (!Double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    continue;
                }

                string metricName = left;
                Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                int braceStart = left.IndexOf('{');
                if (braceStart >= 0)
                {
                    metricName = left.Substring(0, braceStart);
                    int braceEnd = left.LastIndexOf('}');
                    if (braceEnd > braceStart)
                    {
                        labels = ParseLabels(left.Substring(braceStart + 1, braceEnd - braceStart - 1));
                    }
                }

                if (!metricName.StartsWith("vllm", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                metrics.Add(new VllmMetricTelemetry
                {
                    Name = metricName,
                    Labels = labels,
                    Value = value
                });
            }

            return metrics;
        }

        private static Dictionary<string, string> ParseLabels(string input)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> segments = new List<string>();
            bool inQuotes = false;
            bool escaped = false;
            int start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char current = input[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (current == ',' && !inQuotes)
                {
                    segments.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }

            segments.Add(input.Substring(start));

            foreach (string segment in segments)
            {
                string[] pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2)
                {
                    continue;
                }

                labels[pair[0].Trim()] = pair[1].Trim().Trim('"').Replace("\\\"", "\"", StringComparison.Ordinal);
            }

            return labels;
        }

        private static List<string> ExtractModelNames(List<VllmMetricTelemetry> metrics)
        {
            SortedSet<string> modelNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (VllmMetricTelemetry metric in metrics)
            {
                if (metric.Labels.TryGetValue("model_name", out string? modelName)
                    && !String.IsNullOrWhiteSpace(modelName))
                {
                    modelNames.Add(modelName);
                }
                else if (metric.Labels.TryGetValue("model", out string? model)
                    && !String.IsNullOrWhiteSpace(model))
                {
                    modelNames.Add(model);
                }
            }

            return modelNames.ToList();
        }

        private static VllmSummaryTelemetry BuildSummary(List<VllmMetricTelemetry> metrics)
        {
            return new VllmSummaryTelemetry
            {
                RunningRequests = SumMetric(metrics, "vllm:num_requests_running"),
                WaitingRequests = SumMetric(metrics, "vllm:num_requests_waiting"),
                SwappedRequests = SumMetric(metrics, "vllm:num_requests_swapped"),
                GpuCacheUsagePercent = PercentMetric(metrics, "vllm:gpu_cache_usage_perc"),
                CpuCacheUsagePercent = PercentMetric(metrics, "vllm:cpu_cache_usage_perc"),
                PromptTokensTotal = SumMetric(metrics, "vllm:prompt_tokens_total"),
                GenerationTokensTotal = SumMetric(metrics, "vllm:generation_tokens_total"),
                SuccessfulRequestsTotal = SumMetric(metrics, "vllm:request_success_total")
            };
        }

        private static double? SumMetric(List<VllmMetricTelemetry> metrics, string name)
        {
            List<VllmMetricTelemetry> matches = metrics
                .Where(metric => String.Equals(metric.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count < 1)
            {
                return null;
            }

            return matches.Sum(metric => metric.Value);
        }

        private static double? PercentMetric(List<VllmMetricTelemetry> metrics, string name)
        {
            double? value = SumMetric(metrics, name);
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value <= 1D ? value.Value * 100D : value.Value;
        }
    }
}
