namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
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

            try
            {
                using (HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_Settings.RequestTimeoutMs) })
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, healthUri))
                using (HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
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

                _ReaderTask = Task.Run(() => ReadLoopAsync(_CancellationTokenSource.Token));
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            Uri liveUri = BuildLiveUri();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (ClientWebSocket socket = new ClientWebSocket())
                    using (CancellationTokenSource connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        connectCancellation.CancelAfter(_Settings.RequestTimeoutMs);
                        await socket.ConnectAsync(liveUri, connectCancellation.Token).ConfigureAwait(false);

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
                                ApplyEvent(eventDto);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
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
