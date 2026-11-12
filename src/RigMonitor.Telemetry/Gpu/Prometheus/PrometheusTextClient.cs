namespace RigMonitor.Telemetry.Gpu.Prometheus
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;

    /// <summary>
    /// HTTP client that retrieves Prometheus text exposition and records an integration span.
    /// </summary>
    public class PrometheusTextClient : IPrometheusTextClient
    {
        private readonly HttpClient _HttpClient;
        private readonly string _MetricsUrl;
        private readonly string _ServiceName;
        private readonly string _DisplayName;

        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="serviceName">Integration service name used on spans and metrics.</param>
        /// <param name="displayName">Display name used in error descriptions.</param>
        /// <param name="metricsUrl">Metrics URL.</param>
        /// <param name="timeoutMs">Request timeout in milliseconds.</param>
        public PrometheusTextClient(string serviceName, string displayName, string metricsUrl, int timeoutMs)
        {
            if (String.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException(nameof(serviceName));

            _ServiceName = serviceName;
            _DisplayName = String.IsNullOrWhiteSpace(displayName) ? serviceName : displayName;
            _MetricsUrl = metricsUrl ?? String.Empty;
            _HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs))
            };
        }

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Endpoint
        {
            get
            {
                return _MetricsUrl;
            }
        }

        /// <summary>
        /// Retrieve raw Prometheus metrics from the configured endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Metrics text on success, or null when unavailable.</returns>
        public async Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken)
        {
            Uri.TryCreate(_MetricsUrl, UriKind.Absolute, out Uri? target);
            using (IntegrationCall call = IntegrationCall.Start(_ServiceName, "metrics", target))
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _MetricsUrl))
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        call.SetHttpStatusCode((int)response.StatusCode);
                        if (!response.IsSuccessStatusCode)
                        {
                            call.Fail(RigMonitorTelemetryNames.OutcomeHttpError, _DisplayName + " returned HTTP " + (int)response.StatusCode + ".");
                            return null;
                        }

                        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        call.SetTag("http.response.body.size", body.Length);
                        call.Succeed();
                        return body;
                    }
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    call.Fail(exception, cancellationToken);
                    return null;
                }
                catch (OperationCanceledException exception)
                {
                    call.Fail(exception, cancellationToken);
                    throw;
                }
                catch (Exception exception)
                {
                    call.Fail(exception, cancellationToken);
                    return null;
                }
            }
        }
    }
}
