namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Utilyze telemetry provider backed by the Utilyze live WebSocket service.
    /// </summary>
    public class UtilyzeTelemetryProvider : IUtilyzeTelemetryProvider
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly TelemetrySettings _Settings;
        private readonly object _StateLock = new object();
        private readonly CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();
        private Task? _ReaderTask = null;
        private List<int> _DeviceIds = new List<int>();
        private UtilyzeMetricsSnapshotDto? _LatestSnapshot = null;
        private Dictionary<int, UtilyzeGpuCeilingDto> _Ceilings = new Dictionary<int, UtilyzeGpuCeilingDto>();
        private DateTime _LatestReceivedUtc = DateTime.MinValue;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public UtilyzeTelemetryProvider(TelemetrySettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Determine whether Utilyze is reachable.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when Utilyze is enabled and reachable.</returns>
        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            if (!_Settings.UtilyzeEnabled)
            {
                return false;
            }

            Uri? healthUri = BuildHealthUri();
            if (healthUri == null)
            {
                return false;
            }

            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceUtilyze, "health", healthUri))
            {
                try
                {
                    using (HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_Settings.RequestTimeoutMs) })
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, healthUri))
                    using (HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        call.SetHttpStatusCode((int)response.StatusCode);
                        if (!response.IsSuccessStatusCode)
                        {
                            call.Fail(RigMonitorTelemetryNames.OutcomeHttpError, "Utilyze health endpoint returned HTTP " + (int)response.StatusCode + ".");
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
        /// Capture the latest Utilyze telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Latest Utilyze telemetry when available and fresh.</returns>
        public Task<UtilyzeTelemetry?> GetTelemetryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_Settings.UtilyzeEnabled)
            {
                return Task.FromResult<UtilyzeTelemetry?>(null);
            }

            EnsureReaderStarted();

            lock (_StateLock)
            {
                if (_LatestSnapshot == null)
                {
                    return Task.FromResult<UtilyzeTelemetry?>(null);
                }

                DateTime now = DateTime.UtcNow;
                if ((now - _LatestReceivedUtc).TotalMilliseconds > _Settings.UtilyzeSampleStaleAfterMs)
                {
                    return Task.FromResult<UtilyzeTelemetry?>(null);
                }

                return Task.FromResult<UtilyzeTelemetry?>(BuildTelemetry(_LatestSnapshot, _DeviceIds, _Ceilings));
            }
        }

        private void EnsureReaderStarted()
        {
            lock (_StateLock)
            {
                if (_ReaderTask != null && !_ReaderTask.IsCompleted)
                {
                    return;
                }

                // Background hand-off: the reader outlives the request that started it, so it runs as its own
                // trace root and links back to the triggering span instead of inflating the request trace.
                ActivityContext? trigger = Activity.Current?.Context;
                _ReaderTask = Task.Run(() => ReadLoopAsync(trigger, _CancellationTokenSource.Token));
            }
        }

        private async Task ReadLoopAsync(ActivityContext? trigger, CancellationToken cancellationToken)
        {
            Activity.Current = null;
            Uri liveUri = BuildLiveUri();
            Uri displayUri = new UriBuilder(liveUri) { Query = String.Empty }.Uri;

            while (!cancellationToken.IsCancellationRequested)
            {
                bool connected = false;
                using (Activity? connectionActivity = RigMonitorInstrumentation.StartRootActivity("utilyze connection", ActivityKind.Client, trigger))
                {
                    trigger = null;
                    connectionActivity?.SetTag(RigMonitorTelemetryNames.LabelService, RigMonitorTelemetryNames.ServiceUtilyze);
                    connectionActivity?.SetTag("server.address", displayUri.Host);
                    connectionActivity?.SetTag("server.port", displayUri.Port);
                    long messageCount = 0L;

                    try
                    {
                        using (ClientWebSocket socket = new ClientWebSocket())
                        using (CancellationTokenSource connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            connectCancellation.CancelAfter(_Settings.RequestTimeoutMs);
                            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceUtilyze, "connect", displayUri))
                            {
                                try
                                {
                                    await socket.ConnectAsync(liveUri, connectCancellation.Token).ConfigureAwait(false);
                                    call.Succeed();
                                }
                                catch (Exception exception)
                                {
                                    call.Fail(exception, cancellationToken);
                                    throw;
                                }
                            }

                            connected = true;
                            RigMonitorInstrumentation.SetUtilyzeConnected(true);

                            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                            {
                                string? message = await ReceiveMessageAsync(socket, cancellationToken).ConfigureAwait(false);
                                if (String.IsNullOrWhiteSpace(message))
                                {
                                    continue;
                                }

                                UtilyzeEventDto? eventDto = JsonSerializer.Deserialize<UtilyzeEventDto>(message, _JsonOptions);
                                if (eventDto != null)
                                {
                                    messageCount++;
                                    RigMonitorInstrumentation.RecordUtilyzeMessage(ClassifyEventType(eventDto.Type));
                                    ApplyEvent(eventDto);
                                }
                            }

                            connectionActivity?.SetTag("rigmonitor.utilyze.messages", messageCount);
                            RigMonitorInstrumentation.SetOk(connectionActivity);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        connectionActivity?.SetTag("rigmonitor.utilyze.messages", messageCount);
                        RigMonitorInstrumentation.SetUtilyzeConnected(false);
                        return;
                    }
                    catch (Exception exception)
                    {
                        connectionActivity?.SetTag("rigmonitor.utilyze.messages", messageCount);
                        RigMonitorInstrumentation.SetError(connectionActivity, exception);
                        if (connected)
                        {
                            RigMonitorInstrumentation.RecordError("utilyze.stream", exception);
                        }
                    }
                }

                RigMonitorInstrumentation.SetUtilyzeConnected(false);

                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private static string ClassifyEventType(string? type)
        {
            if (String.Equals(type, "init", StringComparison.OrdinalIgnoreCase)) return "init";
            if (String.Equals(type, "metrics", StringComparison.OrdinalIgnoreCase)) return "metrics";
            if (String.Equals(type, "ceilings", StringComparison.OrdinalIgnoreCase)) return "ceilings";
            return "unknown";
        }

        private void ApplyEvent(UtilyzeEventDto eventDto)
        {
            lock (_StateLock)
            {
                if (String.Equals(eventDto.Type, "init", StringComparison.OrdinalIgnoreCase))
                {
                    _DeviceIds = eventDto.DeviceIds ?? new List<int>();
                }
                else if (String.Equals(eventDto.Type, "metrics", StringComparison.OrdinalIgnoreCase)
                    && eventDto.Snapshot != null)
                {
                    _LatestSnapshot = eventDto.Snapshot;
                    _LatestReceivedUtc = DateTime.UtcNow;
                }
                else if (String.Equals(eventDto.Type, "ceilings", StringComparison.OrdinalIgnoreCase)
                    && eventDto.Ceilings != null)
                {
                    _Ceilings = new Dictionary<int, UtilyzeGpuCeilingDto>(eventDto.Ceilings);
                }
            }
        }

        private UtilyzeTelemetry BuildTelemetry(
            UtilyzeMetricsSnapshotDto snapshot,
            List<int> deviceIds,
            Dictionary<int, UtilyzeGpuCeilingDto> ceilings)
        {
            List<UtilyzeDeviceTelemetry> devices = new List<UtilyzeDeviceTelemetry>();
            foreach (UtilyzeGpuSnapshotDto gpu in snapshot.Gpus ?? new List<UtilyzeGpuSnapshotDto>())
            {
                ceilings.TryGetValue(gpu.DeviceId, out UtilyzeGpuCeilingDto? ceiling);

                devices.Add(new UtilyzeDeviceTelemetry
                {
                    DeviceIndex = gpu.DeviceId,
                    Online = true,
                    ComputeSolPercent = gpu.Sol != null && gpu.Sol.Valid ? gpu.Sol.ComputePct : null,
                    MemorySolPercent = gpu.Sol != null && gpu.Sol.Valid ? gpu.Sol.MemoryPct : null,
                    SmActivePercent = gpu.DcgmUtilization != null && gpu.DcgmUtilization.Valid ? gpu.DcgmUtilization.SmActivePct : null,
                    NvmlUtilizationPercent = gpu.NvmlUtilization != null && gpu.NvmlUtilization.Valid ? gpu.NvmlUtilization.UtilPct : null,
                    PcieTransmitBytesPerSecond = gpu.Bandwidth != null && gpu.Bandwidth.Valid ? gpu.Bandwidth.PcieTxBps : null,
                    PcieReceiveBytesPerSecond = gpu.Bandwidth != null && gpu.Bandwidth.Valid ? gpu.Bandwidth.PcieRxBps : null,
                    NvlinkTransmitBytesPerSecond = gpu.Bandwidth != null && gpu.Bandwidth.Valid ? gpu.Bandwidth.NvlinkTxBps : null,
                    NvlinkReceiveBytesPerSecond = gpu.Bandwidth != null && gpu.Bandwidth.Valid ? gpu.Bandwidth.NvlinkRxBps : null,
                    ModelName = ceiling == null ? null : ceiling.ModelName,
                    ComputeSolCeilingPercent = ceiling == null ? null : ceiling.ComputeSolCeiling
                });
            }

            return new UtilyzeTelemetry
            {
                Available = true,
                Endpoint = _Settings.UtilyzeLiveUrl,
                CollectedUtc = snapshot.Timestamp.ToUniversalTime(),
                DeviceIds = deviceIds.Count > 0
                    ? new List<int>(deviceIds)
                    : devices.Select(node => node.DeviceIndex).OrderBy(node => node).ToList(),
                Devices = devices.OrderBy(node => node.DeviceIndex).ToList()
            };
        }

        private async Task<string?> ReceiveMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8192];
            using (MemoryStream stream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private Uri BuildLiveUri()
        {
            UriBuilder builder = new UriBuilder(_Settings.UtilyzeLiveUrl);
            if (String.Equals(builder.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                builder.Scheme = "ws";
            }
            else if (String.Equals(builder.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                builder.Scheme = "wss";
            }

            string clientId = String.IsNullOrWhiteSpace(_Settings.UtilyzeClientId)
                ? "rigmonitor"
                : _Settings.UtilyzeClientId;
            string prefix = String.IsNullOrWhiteSpace(builder.Query) ? String.Empty : builder.Query.TrimStart('?') + "&";
            builder.Query = prefix + "client_id=" + Uri.EscapeDataString(clientId);
            return builder.Uri;
        }

        private Uri? BuildHealthUri()
        {
            try
            {
                UriBuilder builder = new UriBuilder(_Settings.UtilyzeLiveUrl);
                if (String.Equals(builder.Scheme, "ws", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Scheme = Uri.UriSchemeHttp;
                }
                else if (String.Equals(builder.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Scheme = Uri.UriSchemeHttps;
                }

                builder.Path = "/healthz";
                builder.Query = String.Empty;
                return builder.Uri;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private class UtilyzeEventDto
        {
            public string Type { get; set; } = String.Empty;

            public List<int>? DeviceIds { get; set; } = null;

            public UtilyzeMetricsSnapshotDto? Snapshot { get; set; } = null;

            public Dictionary<int, UtilyzeGpuCeilingDto>? Ceilings { get; set; } = null;
        }

        private class UtilyzeMetricsSnapshotDto
        {
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;

            public List<UtilyzeGpuSnapshotDto>? Gpus { get; set; } = null;
        }

        private class UtilyzeGpuSnapshotDto
        {
            public int DeviceId { get; set; } = 0;

            public UtilyzeSolSnapshotDto? Sol { get; set; } = null;

            public UtilyzeBandwidthSnapshotDto? Bandwidth { get; set; } = null;

            public UtilyzeDcgmUtilizationSnapshotDto? DcgmUtilization { get; set; } = null;

            public UtilyzeNvmlUtilizationSnapshotDto? NvmlUtilization { get; set; } = null;
        }

        private class UtilyzeSolSnapshotDto
        {
            public double ComputePct { get; set; } = 0D;

            public double MemoryPct { get; set; } = 0D;

            public bool Valid { get; set; } = false;
        }

        private class UtilyzeBandwidthSnapshotDto
        {
            public double PcieTxBps { get; set; } = 0D;

            public double PcieRxBps { get; set; } = 0D;

            public double NvlinkTxBps { get; set; } = 0D;

            public double NvlinkRxBps { get; set; } = 0D;

            public bool Valid { get; set; } = false;
        }

        private class UtilyzeDcgmUtilizationSnapshotDto
        {
            public double SmActivePct { get; set; } = 0D;

            public bool Valid { get; set; } = false;
        }

        private class UtilyzeNvmlUtilizationSnapshotDto
        {
            public double UtilPct { get; set; } = 0D;

            public bool Valid { get; set; } = false;
        }

        private class UtilyzeGpuCeilingDto
        {
            public int Index { get; set; } = 0;

            public string? ModelName { get; set; } = null;

            public double? ComputeSolCeiling { get; set; } = null;
        }
    }
}
