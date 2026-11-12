namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Nvidia;
    using RigMonitor.Telemetry.Services;
    using Test.Shared;

    /// <summary>
    /// Verifies the snapshot workflow, per-section stages, capability probing, and the no-listener path emit
    /// the documented metrics and spans.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class ObservabilityWorkflowTests
    {
        /// <summary>
        /// A full snapshot emits a root span, one child stage span per section, and section/snapshot metrics.
        /// </summary>
        [Fact]
        public async Task SnapshotShouldEmitRootSpanStageSpansAndMetrics()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TestTelemetryComponents components = CreateComponents();
                components.CpuTelemetryHandler = _ => Task.FromResult(new CpuTelemetry { LogicalCoreCount = 8 });
                TelemetryService service = CreateService(components);

                await service.GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);

                Activity root = Assert.Single(capture.Spans(RigMonitorTelemetryNames.SpanSnapshot));
                Assert.Equal(ActivityStatusCode.Ok, root.Status);
                Assert.Equal("full", root.GetTagItem(RigMonitorTelemetryNames.LabelSnapshotScope));

                Activity cpu = Assert.Single(capture.Spans("stage:cpu"));
                Assert.Equal(root.SpanId, cpu.ParentSpanId);
                Assert.Equal(root.TraceId, cpu.TraceId);
                Assert.Equal(ActivityStatusCode.Ok, cpu.Status);
                Assert.Equal("ok", cpu.GetTagItem(RigMonitorTelemetryNames.LabelSectionStatus));

                foreach (string section in new[] { "system", "cpu", "memory", "network", "disk", "gpu", "ollama", "vllm", "utilyze" })
                {
                    Assert.NotEmpty(capture.Spans("stage:" + section));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.SectionDuration, RigMonitorTelemetryNames.LabelSection, section));
                }

                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "cpu", RigMonitorTelemetryNames.LabelSectionStatus, "ok"));
                Assert.Single(capture.Find(RigMonitorTelemetryNames.SnapshotDuration, RigMonitorTelemetryNames.LabelSnapshotScope, "full"));

                RecordedMeasurement duration = capture.Find(RigMonitorTelemetryNames.SectionDuration).First();
                Assert.Equal("s", duration.Unit);

                capture.CollectObservables();
                Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.SectionLastSuccess, RigMonitorTelemetryNames.LabelSection, "cpu"));
            }
        }

        /// <summary>
        /// Section failures surface as error, unavailable, timeout, and unsupported statuses on metrics and spans,
        /// and the snapshot is recorded as degraded.
        /// </summary>
        [Fact]
        public async Task SectionFailuresShouldBeVisibleOnMetricsAndSpans()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TestTelemetryComponents components = CreateComponents();
                components.CpuTelemetryHandler = _ => Task.FromException<CpuTelemetry>(new InvalidOperationException("cpu sampler failed"));
                components.NvidiaTelemetryHandler = _ => Task.FromResult<NvidiaGpuTelemetry?>(null);
                components.MemoryTelemetryHandler = _ => Task.FromException<MemoryTelemetry>(new TaskCanceledException("timed out"));
                components.DiskTelemetryHandler = _ => Task.FromException<DiskTelemetry>(new PlatformNotSupportedException("no disk counters"));
                TelemetryService service = CreateService(components);

                TelemetrySnapshot snapshot = await service.GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);
                Assert.Equal(TelemetryCollectionStatusCodeEnum.Error, snapshot.Collection!.Cpu.StatusCode);

                Activity cpu = Assert.Single(capture.Spans("stage:cpu"));
                Assert.Equal(ActivityStatusCode.Error, cpu.Status);
                Assert.Equal(typeof(InvalidOperationException).FullName, cpu.GetTagItem(RigMonitorTelemetryNames.LabelErrorType));
                Assert.Contains(cpu.Events, e => e.Name == "exception");

                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "cpu", RigMonitorTelemetryNames.LabelSectionStatus, "error"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "gpu", RigMonitorTelemetryNames.LabelSectionStatus, "unavailable"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "memory", RigMonitorTelemetryNames.LabelSectionStatus, "timeout"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "disk", RigMonitorTelemetryNames.LabelSectionStatus, "unsupported"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.Errors, RigMonitorTelemetryNames.LabelComponent, "section.cpu", RigMonitorTelemetryNames.LabelErrorType, typeof(InvalidOperationException).FullName!));

                Activity root = Assert.Single(capture.Spans(RigMonitorTelemetryNames.SpanSnapshot));
                Assert.Equal("degraded", root.GetTagItem(RigMonitorTelemetryNames.LabelOutcome));
                Assert.Single(capture.Find(RigMonitorTelemetryNames.SnapshotCount, RigMonitorTelemetryNames.LabelOutcome, "degraded"));
            }
        }

        /// <summary>
        /// A partial request records disabled sections and a partial-scope snapshot.
        /// </summary>
        [Fact]
        public async Task PartialSnapshotShouldRecordDisabledSections()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TelemetryService service = CreateService(CreateComponents());
                TelemetryRequestOptions options = TelemetryRequestOptions.All();
                options.IncludeGpu = false;

                await service.GetSnapshotAsync(options, CancellationToken.None);

                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "gpu", RigMonitorTelemetryNames.LabelSectionStatus, "disabled"));
                Assert.Empty(capture.Spans("stage:gpu"));
                Assert.Single(capture.Find(RigMonitorTelemetryNames.SnapshotCount, RigMonitorTelemetryNames.LabelSnapshotScope, "partial"));
            }
        }

        /// <summary>
        /// Caller cancellation is recorded as a cancelled snapshot without an error status.
        /// </summary>
        [Fact]
        public async Task CancelledSnapshotShouldNotBeRecordedAsError()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                TestTelemetryComponents components = CreateComponents();
                components.SystemTelemetryHandler = token =>
                {
                    cts.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(new SystemTelemetry());
                };
                TelemetryService service = CreateService(components);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSnapshotAsync(TelemetryRequestOptions.All(), cts.Token));

                Activity root = Assert.Single(capture.Spans(RigMonitorTelemetryNames.SpanSnapshot));
                Assert.NotEqual(ActivityStatusCode.Error, root.Status);
                Assert.Single(capture.Find(RigMonitorTelemetryNames.SnapshotCount, RigMonitorTelemetryNames.LabelOutcome, "cancelled"));
            }
        }

        /// <summary>
        /// Warmup emits a span and the warm gauge.
        /// </summary>
        [Fact]
        public async Task WarmupShouldEmitSpanAndWarmGauge()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TelemetryService service = CreateService(CreateComponents(), new TelemetrySettings { WarmupDelayMs = 0 });
                await service.WarmupAsync(CancellationToken.None);

                Assert.Equal(ActivityStatusCode.Ok, Assert.Single(capture.Spans(RigMonitorTelemetryNames.SpanWarmup)).Status);
                capture.CollectObservables();
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.TelemetryWarm), m => m.Value == 1D);
            }
        }

        /// <summary>
        /// Capability probing emits a span with probe children and one availability gauge per capability.
        /// </summary>
        [Fact]
        public async Task CapabilityProbeShouldEmitSpanAndGauges()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TelemetrySettings settings = new TelemetrySettings
                {
                    DcgmExporterUrl = "http://127.0.0.1:" + StubHttpServer.GetFreePort() + "/metrics",
                    RequestTimeoutMs = 500
                };
                TestTelemetryComponents components = CreateComponents();
                components.OllamaAvailabilityHandler = _ => Task.FromResult(true);
                RuntimeCapabilitiesService service = new RuntimeCapabilitiesService(
                    settings,
                    false,
                    new NvidiaDcgmGpuTelemetryProvider(settings, new DcgmExporterClient(settings)),
                    components,
                    components,
                    components,
                    components,
                    components,
                    components);

                await service.InitializeAsync(CancellationToken.None);

                Activity probe = Assert.Single(capture.Spans(RigMonitorTelemetryNames.SpanCapabilitiesProbe));
                Activity dcgm = Assert.Single(capture.Spans("dcgm metrics"));
                Assert.Equal(probe.SpanId, dcgm.ParentSpanId);

                capture.CollectObservables();
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.CapabilityAvailable, RigMonitorTelemetryNames.LabelCapability, "nvidia"), m => m.Value == 0D);
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.CapabilityAvailable, RigMonitorTelemetryNames.LabelCapability, "ollama"), m => m.Value == 1D);
            }
        }

        /// <summary>
        /// With no listener attached, every instrumentation entry point is a safe no-op.
        /// </summary>
        [Fact]
        public async Task NoListenerPathShouldNotThrow()
        {
            Assert.Null(RigMonitorInstrumentation.StartActivity("no-listener"));
            Assert.Null(RigMonitorInstrumentation.StartRootActivity("no-listener", ActivityKind.Internal, null));

            using (IntegrationCall call = IntegrationCall.Start("test", "no_listener"))
            {
                Assert.Null(call.Activity);
                call.SetTag("key", "value");
                call.SetHttpStatusCode(200);
            }

            IntegrationCall failed = IntegrationCall.Start("test", "no_listener");
            failed.Fail(new InvalidOperationException("boom"), CancellationToken.None);
            failed.Dispose();
            failed.Dispose();

            RigMonitorInstrumentation.SetOk(null);
            RigMonitorInstrumentation.SetError(null, new InvalidOperationException("boom"));
            RigMonitorInstrumentation.RecordSnapshot("full", "ok", 0.1D);
            RigMonitorInstrumentation.RecordSection("cpu", "ok", null);
            RigMonitorInstrumentation.RecordCollectorJob("ok", 0.1D);
            RigMonitorInstrumentation.RecordSamplesDeleted("api", 0L);
            RigMonitorInstrumentation.RecordError("test", (Exception?)null);
            RigMonitorInstrumentation.SetSectionLastSuccess("cpu", DateTime.UtcNow);

            TelemetryService service = CreateService(CreateComponents());
            TelemetrySnapshot snapshot = await service.GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);
            Assert.NotNull(snapshot.Collection);
        }

        /// <summary>
        /// Exception classification distinguishes timeouts from caller cancellation.
        /// </summary>
        [Fact]
        public void ClassifyExceptionShouldDistinguishTimeoutFromCancellation()
        {
            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Equal("timeout", RigMonitorInstrumentation.ClassifyException(new TaskCanceledException(), CancellationToken.None));
                Assert.Equal("cancelled", RigMonitorInstrumentation.ClassifyException(new TaskCanceledException(), cancelled.Token));
                Assert.Equal("timeout", RigMonitorInstrumentation.ClassifyException(new TimeoutException(), CancellationToken.None));
                Assert.Equal("error", RigMonitorInstrumentation.ClassifyException(new InvalidOperationException(), CancellationToken.None));
            }
        }

        /// <summary>
        /// Build info and configuration gauges are published, and metric labels stay within the documented key set.
        /// </summary>
        [Fact]
        public void GaugesShouldPublishBuildInfoAndSafeConfiguration()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                RigMonitorInstrumentation.SetConfiguration(15D, 2592000D, 5D, 15D, true);
                capture.CollectObservables();

                RecordedMeasurement build = Assert.Single(capture.Find(RigMonitorTelemetryNames.BuildInfo));
                Assert.Equal(1D, build.Value);
                Assert.Equal(RigMonitorInstrumentation.Version, build.Tags[RigMonitorTelemetryNames.LabelVersion]);
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.ConfigCollectionInterval), m => m.Value == 15D);
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.ConfigPersistenceEnabled), m => m.Value == 1D);
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.DatabaseLockCapacity), m => m.Value == 1D);
            }
        }

        private static TelemetryService CreateService(TestTelemetryComponents components, TelemetrySettings? settings = null)
        {
            return new TelemetryService(
                settings ?? new TelemetrySettings(),
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components);
        }

        private static TestTelemetryComponents CreateComponents()
        {
            return new TestTelemetryComponents
            {
                Current = new RuntimeCapabilities
                {
                    HostPlatform = HostPlatformEnum.Linux,
                    NvidiaAvailable = true,
                    OllamaAvailable = true,
                    VllmAvailable = true,
                    UtilyzeAvailable = true
                },
                NvidiaTelemetryHandler = _ => Task.FromResult<NvidiaGpuTelemetry?>(new NvidiaGpuTelemetry { Devices = { new NvidiaGpuDeviceTelemetry() } }),
                OllamaTelemetryHandler = _ => Task.FromResult<OllamaTelemetry?>(new OllamaTelemetry { Available = true }),
                VllmTelemetryHandler = _ => Task.FromResult<VllmTelemetry?>(new VllmTelemetry { Available = true }),
                UtilyzeTelemetryHandler = _ => Task.FromResult<UtilyzeTelemetry?>(new UtilyzeTelemetry { Available = true })
            };
        }
    }
}
