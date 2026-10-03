namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Services;
    using Test.Shared;

    /// <summary>
    /// Verifies every outbound integration emits a client span, the integration counter and latency histogram,
    /// error classification, and W3C trace context propagation to the downstream service.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class ObservabilityIntegrationTests
    {
        private const string _DcgmMetrics = "DCGM_FI_DEV_GPU_UTIL{gpu=\"0\",UUID=\"GPU-1\",modelName=\"Test GPU\"} 42\n";

        /// <summary>
        /// A successful DCGM call records an ok client span and propagates the trace to the exporter.
        /// </summary>
        [Fact]
        public async Task DcgmSuccessShouldRecordClientSpanAndPropagateContext()
        {
            using (StubHttpServer server = new StubHttpServer(Routes("/metrics", 200, _DcgmMetrics)))
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                DcgmExporterClient client = new DcgmExporterClient(new TelemetrySettings { DcgmExporterUrl = server.BaseUrl + "/metrics" });

                string? metrics = await client.TryGetMetricsAsync(CancellationToken.None);

                Assert.Equal(_DcgmMetrics, metrics);
                Activity span = Assert.Single(capture.Spans("dcgm metrics"));
                Assert.Equal(ActivityKind.Client, span.Kind);
                Assert.Equal(ActivityStatusCode.Ok, span.Status);
                Assert.Equal(200, span.GetTagItem("http.response.status_code"));
                Assert.Equal("127.0.0.1", span.GetTagItem("server.address"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "dcgm", RigMonitorTelemetryNames.LabelOperation, "metrics", RigMonitorTelemetryNames.LabelOutcome, "ok"));
                Assert.Single(capture.Find(RigMonitorTelemetryNames.IntegrationDuration, RigMonitorTelemetryNames.LabelService, "dcgm"));

                string? traceparent = Assert.Single(server.TraceParents);
                Assert.NotNull(traceparent);
                Assert.Contains(span.TraceId.ToHexString(), traceparent);
            }
        }

        /// <summary>
        /// A non-success HTTP status is recorded as http_error with an error span.
        /// </summary>
        [Fact]
        public async Task DcgmHttpErrorShouldBeRecorded()
        {
            using (StubHttpServer server = new StubHttpServer(Routes("/metrics", 503, "unavailable")))
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                DcgmExporterClient client = new DcgmExporterClient(new TelemetrySettings { DcgmExporterUrl = server.BaseUrl + "/metrics" });

                Assert.Null(await client.TryGetMetricsAsync(CancellationToken.None));

                Activity span = Assert.Single(capture.Spans("dcgm metrics"));
                Assert.Equal(ActivityStatusCode.Error, span.Status);
                Assert.Equal(503, span.GetTagItem("http.response.status_code"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "dcgm", RigMonitorTelemetryNames.LabelOutcome, "http_error"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.Errors, RigMonitorTelemetryNames.LabelComponent, "dcgm", RigMonitorTelemetryNames.LabelErrorType, "http_error"));
            }
        }

        /// <summary>
        /// An unreachable dependency is recorded as an error with the exception type and an exception event.
        /// </summary>
        [Fact]
        public async Task UnreachableDependencyShouldRecordExceptionType()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                VllmTelemetryProvider provider = new VllmTelemetryProvider(new TelemetrySettings
                {
                    VllmEnabled = true,
                    VllmMetricsUrl = "http://127.0.0.1:" + StubHttpServer.GetFreePort() + "/metrics",
                    RequestTimeoutMs = 500
                });

                Assert.False(await provider.IsAvailableAsync(CancellationToken.None));

                Activity span = Assert.Single(capture.Spans("vllm metrics"));
                Assert.Equal(ActivityStatusCode.Error, span.Status);
                Assert.Contains(span.Events, e => e.Name == "exception");
                Assert.Equal(typeof(System.Net.Http.HttpRequestException).FullName, span.GetTagItem(RigMonitorTelemetryNames.LabelErrorType));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "vllm", RigMonitorTelemetryNames.LabelOutcome, "error"));
            }
        }

        /// <summary>
        /// A successful vLLM scrape records an ok integration call.
        /// </summary>
        [Fact]
        public async Task VllmSuccessShouldRecordOkCall()
        {
            using (StubHttpServer server = new StubHttpServer(Routes("/metrics", 200, "vllm:num_requests_running{model_name=\"m\"} 2\n")))
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                VllmTelemetryProvider provider = new VllmTelemetryProvider(new TelemetrySettings { VllmEnabled = true, VllmMetricsUrl = server.BaseUrl + "/metrics" });

                VllmTelemetry? telemetry = await provider.GetTelemetryAsync(CancellationToken.None);

                Assert.NotNull(telemetry);
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "vllm", RigMonitorTelemetryNames.LabelOutcome, "ok"));
            }
        }

        /// <summary>
        /// Ollama telemetry issues three parallel calls, each with its own client span nested under the caller.
        /// </summary>
        [Fact]
        public async Task OllamaShouldRecordOneSpanPerApiCall()
        {
            Dictionary<string, KeyValuePair<int, string>> routes = new Dictionary<string, KeyValuePair<int, string>>
            {
                { "/api/version", new KeyValuePair<int, string>(200, "{\"version\":\"0.9.1\"}") },
                { "/api/tags", new KeyValuePair<int, string>(200, "{\"models\":[]}") },
                { "/api/ps", new KeyValuePair<int, string>(200, "{\"models\":[]}") }
            };

            using (StubHttpServer server = new StubHttpServer(routes))
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                OllamaClient client = new OllamaClient(new TelemetrySettings { OllamaBaseUrl = server.BaseUrl });

                using (Activity? parent = RigMonitorInstrumentation.StartActivity("test parent"))
                {
                    OllamaTelemetry? telemetry = await client.GetTelemetryAsync(CancellationToken.None);
                    Assert.NotNull(telemetry);
                    Assert.Equal("0.9.1", telemetry!.Version);

                    foreach (string operation in new[] { "version", "tags", "ps" })
                    {
                        Activity span = Assert.Single(capture.Spans("ollama " + operation));
                        Assert.Equal(parent!.SpanId, span.ParentSpanId);
                        Assert.Equal(ActivityStatusCode.Ok, span.Status);
                    }
                }

                Assert.Equal(3D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "ollama", RigMonitorTelemetryNames.LabelOutcome, "ok"));
                Assert.Equal(3, server.TraceParents.Count(t => t != null));
            }
        }

        /// <summary>
        /// A failing Ollama endpoint records http_error for that call and the probe reports unavailable.
        /// </summary>
        [Fact]
        public async Task OllamaFailureShouldRecordHttpError()
        {
            using (StubHttpServer server = new StubHttpServer(Routes("/api/version", 500, "boom")))
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                OllamaClient client = new OllamaClient(new TelemetrySettings { OllamaBaseUrl = server.BaseUrl });

                Assert.False(await client.IsAvailableAsync(CancellationToken.None));
                Assert.Null(await client.GetTelemetryAsync(CancellationToken.None));

                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "ollama", RigMonitorTelemetryNames.LabelOperation, "probe", RigMonitorTelemetryNames.LabelOutcome, "http_error"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "ollama", RigMonitorTelemetryNames.LabelOperation, "version", RigMonitorTelemetryNames.LabelOutcome, "http_error"));
            }
        }

        /// <summary>
        /// Utilyze health failures are recorded, and the background reader runs as its own root span linked to
        /// the span that started it.
        /// </summary>
        [Fact]
        public async Task UtilyzeShouldRecordHealthAndDetachedConnectionSpans()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                UtilyzeTelemetryProvider provider = new UtilyzeTelemetryProvider(new TelemetrySettings
                {
                    UtilyzeEnabled = true,
                    UtilyzeLiveUrl = "ws://127.0.0.1:" + StubHttpServer.GetFreePort() + "/live",
                    RequestTimeoutMs = 500
                });

                Assert.False(await provider.IsAvailableAsync(CancellationToken.None));
                Assert.Single(capture.Spans("utilyze health"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "utilyze", RigMonitorTelemetryNames.LabelOperation, "health", RigMonitorTelemetryNames.LabelOutcome, "error"));

                ActivityTraceId triggerTrace;
                using (Activity? trigger = RigMonitorInstrumentation.StartActivity("test trigger"))
                {
                    triggerTrace = trigger!.TraceId;
                    Assert.Null(await provider.GetTelemetryAsync(CancellationToken.None));
                }

                Activity? connection = await WaitForSpanAsync(capture, "utilyze connection", TimeSpan.FromSeconds(10));
                Assert.NotNull(connection);
                Assert.Equal(default(ActivitySpanId), connection!.ParentSpanId);
                Assert.NotEqual(triggerTrace, connection.TraceId);
                Assert.Contains(connection.Links, link => link.Context.TraceId == triggerTrace);
                Assert.Equal(ActivityStatusCode.Error, connection.Status);
                Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "utilyze", RigMonitorTelemetryNames.LabelOperation, "connect"));

                capture.CollectObservables();
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.UtilyzeConnected), m => m.Value == 0D);
            }
        }

        /// <summary>
        /// Subprocess execution (macOS memory and CPU sampling) records a process integration call.
        /// </summary>
        [Fact]
        public async Task SubprocessShouldRecordProcessIntegration()
        {
            if (!OperatingSystem.IsMacOS()) return;

            using (TelemetryCapture capture = new TelemetryCapture())
            {
                DefaultMemoryInfoProvider provider = new DefaultMemoryInfoProvider();
                await provider.GetMemoryTelemetryAsync(CancellationToken.None);

                Activity span = Assert.Single(capture.Spans("process sysctl"));
                Assert.Equal(0, span.GetTagItem("process.exit.code"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "process", RigMonitorTelemetryNames.LabelOperation, "sysctl", RigMonitorTelemetryNames.LabelOutcome, "ok"));
            }
        }

        private static Dictionary<string, KeyValuePair<int, string>> Routes(string path, int status, string body)
        {
            return new Dictionary<string, KeyValuePair<int, string>> { { path, new KeyValuePair<int, string>(status, body) } };
        }

        private static async Task<Activity?> WaitForSpanAsync(TelemetryCapture capture, string name, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                Activity? found = capture.Spans(name).FirstOrDefault();
                if (found != null) return found;
                await Task.Delay(50);
            }

            return null;
        }
    }
}
