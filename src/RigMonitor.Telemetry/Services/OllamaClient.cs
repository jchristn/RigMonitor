namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Models.Ollama;

    /// <summary>
    /// Ollama API client.
    /// </summary>
    public class OllamaClient : IOllamaClient
    {
        private readonly HttpClient _HttpClient;
        private readonly string _BaseUrl;

        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public OllamaClient(TelemetrySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _BaseUrl = settings.OllamaBaseUrl.TrimEnd('/');
            _HttpClient = new HttpClient
            {
                BaseAddress = new Uri(_BaseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromMilliseconds(settings.RequestTimeoutMs)
            };
        }

        /// <summary>
        /// Determine whether Ollama is available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when reachable.</returns>
        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (HttpResponseMessage response = await _HttpClient.GetAsync("/api/version", cancellationToken).ConfigureAwait(false))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Capture Ollama telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ollama telemetry when reachable.</returns>
        public async Task<OllamaTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            try
            {
                Task<OllamaVersionResponse?> versionTask = _HttpClient.GetFromJsonAsync<OllamaVersionResponse>("/api/version", cancellationToken);
                Task<OllamaTagsResponse?> tagsTask = _HttpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", cancellationToken);
                Task<OllamaRunningResponse?> runningTask = _HttpClient.GetFromJsonAsync<OllamaRunningResponse>("/api/ps", cancellationToken);

                await Task.WhenAll(versionTask, tagsTask, runningTask).ConfigureAwait(false);

                OllamaVersionResponse? version = await versionTask.ConfigureAwait(false);
                OllamaTagsResponse? tags = await tagsTask.ConfigureAwait(false);
                OllamaRunningResponse? running = await runningTask.ConfigureAwait(false);

                OllamaTelemetry telemetry = new OllamaTelemetry
                {
                    Available = true,
                    BaseUrl = _BaseUrl,
                    Version = version?.Version ?? String.Empty,
                    CollectedUtc = DateTime.UtcNow,
                    AvailableModels = tags?.Models.Select(MapModel).ToList() ?? new System.Collections.Generic.List<OllamaModelTelemetry>(),
                    LoadedModels = running?.Models.Select(MapLoadedModel).ToList() ?? new System.Collections.Generic.List<OllamaLoadedModelTelemetry>()
                };

                telemetry.AvailableModelCount = telemetry.AvailableModels.Count;
                telemetry.LoadedModelCount = telemetry.LoadedModels.Count;
                return telemetry;
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

        private static OllamaModelTelemetry MapModel(OllamaModelResponse response)
        {
            return new OllamaModelTelemetry
            {
                Name = response.Name ?? String.Empty,
                Model = response.Model ?? String.Empty,
                Digest = response.Digest ?? String.Empty,
                ModifiedUtc = response.ModifiedAt,
                SizeBytes = response.Size,
                Family = response.Details?.Family ?? String.Empty,
                Format = response.Details?.Format ?? String.Empty,
                ParameterSize = response.Details?.ParameterSize ?? String.Empty,
                QuantizationLevel = response.Details?.QuantizationLevel ?? String.Empty
            };
        }

        private static OllamaLoadedModelTelemetry MapLoadedModel(OllamaLoadedModelResponse response)
        {
            return new OllamaLoadedModelTelemetry
            {
                Name = response.Name ?? String.Empty,
                Model = response.Model ?? String.Empty,
                Digest = response.Digest ?? String.Empty,
                ExpiresAtUtc = response.ExpiresAt,
                SizeBytes = response.Size,
                SizeVramBytes = response.SizeVram,
                Family = response.Details?.Family ?? String.Empty,
                Format = response.Details?.Format ?? String.Empty,
                ParameterSize = response.Details?.ParameterSize ?? String.Empty,
                QuantizationLevel = response.Details?.QuantizationLevel ?? String.Empty
            };
        }
    }
}
