namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
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
        private readonly Uri _BaseUri;

        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public OllamaClient(TelemetrySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _BaseUrl = settings.OllamaBaseUrl.TrimEnd('/');
            _BaseUri = new Uri(_BaseUrl, UriKind.Absolute);
            _HttpClient = new HttpClient
            {
                BaseAddress = _BaseUri,
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
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceOllama, "probe", _BaseUri))
            {
                try
                {
                    using (HttpResponseMessage response = await _HttpClient.GetAsync("/api/version", cancellationToken).ConfigureAwait(false))
                    {
                        call.SetHttpStatusCode((int)response.StatusCode);
                        if (!response.IsSuccessStatusCode)
                        {
                            call.Fail(RigMonitorTelemetryNames.OutcomeHttpError, "Ollama returned HTTP " + (int)response.StatusCode + ".");
                            return false;
                        }

                        call.Succeed();
                        return true;
                    }
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    call.Fail(exception, cancellationToken);
                    return false;
                }
                catch (OperationCanceledException exception)
                {
                    call.Fail(exception, cancellationToken);
                    throw;
                }
                catch (Exception exception)
                {
                    call.Fail(exception, cancellationToken);
                    return false;
                }
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
                Task<OllamaVersionResponse?> versionTask = GetJsonAsync<OllamaVersionResponse>("/api/version", "version", cancellationToken);
                Task<OllamaTagsResponse?> tagsTask = GetJsonAsync<OllamaTagsResponse>("/api/tags", "tags", cancellationToken);
                Task<OllamaRunningResponse?> runningTask = GetJsonAsync<OllamaRunningResponse>("/api/ps", "ps", cancellationToken);

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

        private async Task<T?> GetJsonAsync<T>(string path, string operation, CancellationToken cancellationToken)
        {
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceOllama, operation, _BaseUri))
            {
                try
                {
                    using (HttpResponseMessage response = await _HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false))
                    {
                        call.SetHttpStatusCode((int)response.StatusCode);
                        if (!response.IsSuccessStatusCode)
                        {
                            call.Fail(RigMonitorTelemetryNames.OutcomeHttpError, "Ollama returned HTTP " + (int)response.StatusCode + ".");
                        }

                        response.EnsureSuccessStatusCode();
                        T? result = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
                        call.Succeed();
                        return result;
                    }
                }
                catch (Exception exception)
                {
                    call.Fail(exception, cancellationToken);
                    throw;
                }
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
