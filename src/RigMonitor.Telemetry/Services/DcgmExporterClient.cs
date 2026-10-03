namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// DCGM exporter HTTP client.
    /// </summary>
    public class DcgmExporterClient : IDcgmExporterClient
    {
        private readonly HttpClient _HttpClient;
        private readonly string _MetricsUrl;

        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public DcgmExporterClient(TelemetrySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _MetricsUrl = settings.DcgmExporterUrl;
            _HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(settings.RequestTimeoutMs)
            };
        }

        /// <summary>
        /// Retrieve raw Prometheus metrics from the configured exporter.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Metrics text on success, or null when unavailable.</returns>
        public async Task<string?> TryGetMetricsAsync(CancellationToken cancellationToken)
        {
            Uri.TryCreate(_MetricsUrl, UriKind.Absolute, out Uri? target);
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceDcgm, "metrics", target))
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _MetricsUrl))
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        call.SetHttpStatusCode((int)response.StatusCode);
                        if (!response.IsSuccessStatusCode)
                        {
                            call.Fail(RigMonitorTelemetryNames.OutcomeHttpError, "DCGM exporter returned HTTP " + (int)response.StatusCode + ".");
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
