namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
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
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _MetricsUrl))
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
    }
}
