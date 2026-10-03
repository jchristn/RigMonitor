namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Radiant;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server;
    using RigMonitor.Server.Serialization;
    using RigMonitor.Server.Services;
    using Test.Shared;

    /// <summary>
    /// Verifies observability settings, environment overrides, the Radiant host at the composition root,
    /// the structured log sink, and an end-to-end trace from Watson's server span into application spans.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class ObservabilityHostTests
    {
        /// <summary>
        /// Defaults use loopback 127.0.0.1 and invalid values clamp or fall back.
        /// </summary>
        [Fact]
        public void SettingsShouldUseLoopbackDefaultsAndClamp()
        {
            ObservabilitySettings settings = new ObservabilitySettings();
            Assert.True(settings.Enabled);
            Assert.Equal("rigmonitor", settings.ServiceName);
            Assert.Equal("http://127.0.0.1:4317", settings.OtlpEndpoint);
            Assert.Equal("grpc", settings.OtlpProtocol);
            Assert.Equal("127.0.0.1", settings.PrometheusHostname);
            Assert.Equal(9464, settings.PrometheusPort);
            Assert.Equal("/metrics", settings.PrometheusPath);
            Assert.False(settings.LokiEnabled);
            Assert.Equal("http://127.0.0.1:3100/otlp", settings.LokiEndpoint);

            settings.PrometheusPort = 99999;
            settings.TraceSamplingRatio = 4D;
            settings.MetricExportIntervalMs = 1;
            settings.OtlpEndpoint = "not a uri";
            settings.OtlpProtocol = "bogus";
            settings.PrometheusPath = "scrape";
            settings.ServiceName = " ";

            Assert.Equal(65535, settings.PrometheusPort);
            Assert.Equal(1D, settings.TraceSamplingRatio);
            Assert.Equal(1000, settings.MetricExportIntervalMs);
            Assert.Equal("http://127.0.0.1:4317", settings.OtlpEndpoint);
            Assert.Equal("grpc", settings.OtlpProtocol);
            Assert.Equal("/scrape", settings.PrometheusPath);
            Assert.Equal("rigmonitor", settings.ServiceName);
        }

        /// <summary>
        /// RIGMONITOR_OBSERVABILITY_* variables override settings; unset or invalid values are ignored.
        /// </summary>
        [Fact]
        public void EnvironmentOverridesShouldApply()
        {
            Dictionary<string, string> environment = new Dictionary<string, string>
            {
                { "RIGMONITOR_OBSERVABILITY_ENABLED", "false" },
                { "RIGMONITOR_OBSERVABILITY_SERVICE_NAME", "rig-a" },
                { "RIGMONITOR_OBSERVABILITY_OTLP_ENDPOINT", "http://tempo:4317" },
                { "RIGMONITOR_OBSERVABILITY_OTLP_PROTOCOL", "httpprotobuf" },
                { "RIGMONITOR_OBSERVABILITY_PROMETHEUS_HOSTNAME", "rigmonitor" },
                { "RIGMONITOR_OBSERVABILITY_PROMETHEUS_PORT", "not-a-number" },
                { "RIGMONITOR_OBSERVABILITY_LOKI_ENABLED", "1" },
                { "RIGMONITOR_OBSERVABILITY_LOKI_ENDPOINT", "http://loki:3100/otlp" },
                { "RIGMONITOR_OBSERVABILITY_TRACE_SAMPLING_RATIO", "0.25" }
            };
            RigMonitorSettings settings = new RigMonitorSettings();

            SettingsManager.ApplyEnvironmentOverrides(settings, name => environment.TryGetValue(name, out string? value) ? value : null);

            Assert.False(settings.Observability.Enabled);
            Assert.Equal("rig-a", settings.Observability.ServiceName);
            Assert.Equal("http://tempo:4317", settings.Observability.OtlpEndpoint);
            Assert.Equal("httpprotobuf", settings.Observability.OtlpProtocol);
            Assert.Equal("rigmonitor", settings.Observability.PrometheusHostname);
            Assert.Equal(9464, settings.Observability.PrometheusPort);
            Assert.True(settings.Observability.LokiEnabled);
            Assert.Equal("http://loki:3100/otlp", settings.Observability.LokiEndpoint);
            Assert.Equal(0.25D, settings.Observability.TraceSamplingRatio);
        }

        /// <summary>
        /// The Radiant settings subscribe to the RigMonitor and Watson sources and mirror the configured exporters.
        /// </summary>
        [Fact]
        public void RadiantSettingsShouldSubscribeToAllSources()
        {
            ObservabilitySettings settings = new ObservabilitySettings
            {
                ServiceName = "rig-b",
                OtlpEndpoint = "http://collector:4318",
                OtlpProtocol = "httpprotobuf",
                PrometheusPort = 9555,
                LokiEnabled = true
            };

            RadiantSettings radiant = ObservabilityService.BuildRadiantSettings(settings);

            Assert.Equal("rig-b", radiant.ServiceName);
            Assert.Contains(RigMonitorTelemetryNames.MeterName, radiant.Sources.MeterNames);
            Assert.Contains(RigMonitorTelemetryNames.WatsonSourceName, radiant.Sources.MeterNames);
            Assert.Contains(RigMonitorTelemetryNames.ActivitySourceName, radiant.Sources.ActivitySourceNames);
            Assert.Contains(RigMonitorTelemetryNames.WatsonSourceName, radiant.Sources.ActivitySourceNames);
            Assert.Equal(OtlpProtocolEnum.HttpProtobuf, radiant.Otlp.Protocol);
            Assert.Equal("http://collector:4318", radiant.Otlp.Endpoint);
            Assert.Equal(9555, radiant.Prometheus.Port);
            Assert.True(radiant.Loki.Enable);
            Assert.True(radiant.Logs.Enable);
            Assert.NotEmpty(radiant.Metrics.Definitions);
        }

        /// <summary>
        /// A started host serves RigMonitor metrics, with second-based histogram buckets, on its scrape endpoint.
        /// </summary>
        [Fact]
        public async Task HostShouldServePrometheusScrape()
        {
            string directory = CreateTempDirectory();
            int port = StubHttpServer.GetFreePort();
            try
            {
                using (ObservabilityService service = new ObservabilityService(
                    new ObservabilitySettings { OtlpEnabled = false, PrometheusPort = port },
                    CreateLogger(directory)))
                {
                    Assert.True(service.IsEnabled);
                    RigMonitorInstrumentation.RecordIntegration("dcgm", "metrics", "ok", 0.02D);

                    using (HttpClient client = new HttpClient())
                    {
                        string body = await client.GetStringAsync("http://127.0.0.1:" + port + "/metrics");
                        Assert.Contains("rigmonitor_build_info", body);
                        Assert.Contains("rigmonitor_integration_requests_total", body);
                        Assert.Contains("rigmonitor_integration_duration_seconds_bucket", body);
                        Assert.Contains("le=\"60\"", body);
                    }
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// Disabled settings start no host; an initialization failure is logged and never thrown.
        /// </summary>
        [Fact]
        public void HostShouldBeBestEffort()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (ObservabilityService disabled = new ObservabilityService(new ObservabilitySettings { Enabled = false }, CreateLogger(directory)))
                {
                    Assert.False(disabled.IsEnabled);
                    Assert.Null(disabled.CreateLogger("x"));
                }

                using (ObservabilityService nullSettings = new ObservabilityService(null, CreateLogger(directory)))
                {
                    Assert.False(nullSettings.IsEnabled);
                }

                // "*" cannot be bound by the Prometheus listener, so startup fails inside Radiant.
                ObservabilityService failed = new ObservabilityService(
                    new ObservabilitySettings { OtlpEnabled = false, PrometheusHostname = "*", PrometheusPort = StubHttpServer.GetFreePort() },
                    CreateLogger(directory));
                Assert.False(failed.IsEnabled);
                failed.Dispose();
                failed.Dispose();
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// The application logger forwards to an attached sink, stamped with the active trace.
        /// </summary>
        [Fact]
        public void AppLoggerShouldForwardToSinkWithTraceContext()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    AppLogger logger = CreateLogger(directory);
                    RecordingLogger sink = new RecordingLogger();
                    logger.AttachSink(sink);

                    string traceId;
                    using (Activity? activity = RigMonitorInstrumentation.StartActivity("log test"))
                    {
                        traceId = activity!.TraceId.ToHexString();
                        logger.Warn("persist failed");
                    }

                    logger.AttachSink(null);
                    logger.Info("after detach");

                    string entry = Assert.Single(sink.Entries);
                    Assert.Equal("Warning|" + traceId + "|persist failed", entry);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// End to end through the real server: Watson adopts an inbound traceparent, application spans nest under
        /// Watson's server span, the background collector runs as its own trace, and the scrape endpoint serves
        /// both Watson and RigMonitor metrics.
        /// </summary>
        [Fact]
        public async Task ServerShouldProduceOneTraceFromHttpToApplicationSpans()
        {
            string directory = CreateTempDirectory();
            int webPort = StubHttpServer.GetFreePort();
            int scrapePort = StubHttpServer.GetFreePort();
            int closedPort = StubHttpServer.GetFreePort();
            string settingsFile = Path.Combine(directory, "rigmonitor.json");

            RigMonitorSettings settings = new RigMonitorSettings();
            settings.Webserver.Hostname = "127.0.0.1";
            settings.Webserver.Port = webPort;
            settings.Telemetry.DcgmExporterUrl = "http://127.0.0.1:" + closedPort + "/metrics";
            settings.Telemetry.OllamaBaseUrl = "http://127.0.0.1:" + closedPort;
            settings.Telemetry.RequestTimeoutMs = 500;
            settings.Telemetry.WarmupDelayMs = 0;
            settings.Dashboard.Enabled = false;
            settings.Logging.LogDirectory = Path.Combine(directory, "logs");
            settings.Logging.ConsoleLogging = false;
            settings.Logging.MinimumSeverity = LogSeverityEnum.Info;
            settings.Persistence.Database.Filename = Path.Combine(directory, "history.db");
            settings.Persistence.CollectionIntervalMs = 60000;
            settings.Observability.OtlpEnabled = false;
            settings.Observability.PrometheusPort = scrapePort;
            await File.WriteAllTextAsync(settingsFile, RigMonitorJsonSerializer.Serialize(settings, true));

            RigMonitorServer? server = null;
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    server = await RigMonitorServer.CreateAsync(settingsFile, CancellationToken.None);
                    await server.StartAsync(CancellationToken.None);

                    string inboundTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
                    using (HttpClient client = new HttpClient())
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:" + webPort + "/v1/telemetry?cpu"))
                    {
                        request.Headers.TryAddWithoutValidation("traceparent", "00-" + inboundTraceId + "-00f067aa0ba902b7-01");
                        using (HttpResponseMessage response = await client.SendAsync(request))
                        {
                            Assert.True(response.IsSuccessStatusCode);
                        }

                        await WaitUntilAsync(() => capture.Spans(RigMonitorTelemetryNames.SpanCollectorJob).Any(), TimeSpan.FromSeconds(10));

                        string scrape = await client.GetStringAsync("http://127.0.0.1:" + scrapePort + "/metrics");
                        Assert.Contains("http_server_request_duration_seconds", scrape);
                        Assert.Contains("watson_server_up", scrape);
                        Assert.Contains("rigmonitor_snapshot_count_total", scrape);
                    }

                    await WaitUntilAsync(() => capture.Activities.Any(a => a.Source.Name == RigMonitorTelemetryNames.WatsonSourceName && a.TraceId.ToHexString() == inboundTraceId), TimeSpan.FromSeconds(5));

                    Activity watson = capture.Activities.First(a => a.Source.Name == RigMonitorTelemetryNames.WatsonSourceName && a.TraceId.ToHexString() == inboundTraceId);
                    Assert.Equal(ActivityKind.Server, watson.Kind);
                    Assert.Equal("00f067aa0ba902b7", watson.ParentSpanId.ToHexString());

                    Activity snapshot = capture.Spans(RigMonitorTelemetryNames.SpanSnapshot).First(a => a.TraceId.ToHexString() == inboundTraceId);
                    Assert.Equal(watson.SpanId, snapshot.ParentSpanId);
                    Assert.Equal("partial", snapshot.GetTagItem(RigMonitorTelemetryNames.LabelSnapshotScope));
                    Assert.Contains(capture.Spans("stage:cpu"), a => a.ParentSpanId == snapshot.SpanId);

                    Activity startup = Assert.Single(capture.Spans(RigMonitorTelemetryNames.SpanStartup));
                    Assert.Equal(ActivityStatusCode.Ok, startup.Status);
                    Assert.Contains(capture.Spans(RigMonitorTelemetryNames.SpanCapabilitiesProbe), a => a.ParentSpanId == startup.SpanId);

                    Activity job = capture.Spans(RigMonitorTelemetryNames.SpanCollectorJob).First();
                    Assert.NotEqual(startup.TraceId, job.TraceId);
                    Assert.NotEqual(inboundTraceId, job.TraceId.ToHexString());
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.StartupDuration, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                }
            }
            finally
            {
                server?.Stop();
                DeleteDirectory(directory);
            }
        }

        private static AppLogger CreateLogger(string directory)
        {
            return new AppLogger(new LoggingSettings
            {
                LogDirectory = Path.Combine(directory, "logs"),
                ConsoleLogging = false,
                FileLogging = false,
                MinimumSeverity = LogSeverityEnum.Debug
            });
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !condition())
            {
                await Task.Delay(50);
            }
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "RigMonitorObservabilityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
