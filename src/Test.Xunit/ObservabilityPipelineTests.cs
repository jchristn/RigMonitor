namespace Test.Xunit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Database;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Services;
    using Test.Shared;

    /// <summary>
    /// Verifies the history database, the background collector pipeline (job, queued/collect/persist stages,
    /// last-success gauge), and the retention job emit the documented metrics and spans, including failures.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class ObservabilityPipelineTests
    {
        /// <summary>
        /// Database operations record sqlite client spans, integration metrics, sample counters, and slot metrics.
        /// </summary>
        [Fact]
        public async Task DatabaseOperationsShouldBeInstrumented()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    PersistenceSettings settings = CreatePersistenceSettings(directory);
                    DatabaseDriverBase database = DatabaseDriverFactory.Create(settings);
                    await database.InitializeAsync(CancellationToken.None);

                    TelemetrySampleDetail sample = await database.TelemetryHistory.CreateAsync(BuildSnapshot(DateTime.UtcNow.AddDays(-10)), CancellationToken.None);
                    await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter(), CancellationToken.None);
                    Assert.True(await database.TelemetryHistory.DeleteAsync(sample.Id, CancellationToken.None));
                    await database.TelemetryHistory.CreateAsync(BuildSnapshot(DateTime.UtcNow.AddDays(-10)), CancellationToken.None);
                    long pruned = await database.TelemetryHistory.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);
                    Assert.Equal(1L, pruned);
                    database.Dispose();

                    Assert.Single(capture.Spans("sqlite initialize"));
                    Activity create = capture.Spans("sqlite create").First();
                    Assert.Equal(ActivityKind.Client, create.Kind);
                    Assert.Equal("sqlite", create.GetTagItem("db.system.name"));
                    Assert.Equal("create", create.GetTagItem("db.operation.name"));

                    foreach (string operation in new[] { "initialize", "create", "search", "delete", "prune" })
                    {
                        Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "sqlite", RigMonitorTelemetryNames.LabelOperation, operation, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                        Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.IntegrationDuration, RigMonitorTelemetryNames.LabelService, "sqlite", RigMonitorTelemetryNames.LabelOperation, operation));
                    }

                    Assert.Equal(2D, capture.Sum(RigMonitorTelemetryNames.SamplesPersisted));
                    Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SamplesDeleted, RigMonitorTelemetryNames.LabelDeleteReason, "api"));
                    Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SamplesDeleted, RigMonitorTelemetryNames.LabelDeleteReason, "retention"));

                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.DatabaseLockWaitDuration, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                    Assert.Equal(0D, capture.Sum(RigMonitorTelemetryNames.DatabaseLockInUse));
                    Assert.Equal(0D, capture.Sum(RigMonitorTelemetryNames.DatabaseLockWaiting));
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// A failing database call records an error outcome and exception on the client span.
        /// </summary>
        [Fact]
        public async Task DatabaseFailureShouldBeRecorded()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    DatabaseDriverBase database = DatabaseDriverFactory.Create(CreatePersistenceSettings(directory));

                    // Not initialized: the tables do not exist, so the insert fails.
                    await Assert.ThrowsAnyAsync<Exception>(() => database.TelemetryHistory.CreateAsync(BuildSnapshot(DateTime.UtcNow), CancellationToken.None));
                    database.Dispose();

                    Activity create = Assert.Single(capture.Spans("sqlite create"));
                    Assert.Equal(ActivityStatusCode.Error, create.Status);
                    Assert.Contains(create.Events, e => e.Name == "exception");
                    Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.IntegrationRequests, RigMonitorTelemetryNames.LabelService, "sqlite", RigMonitorTelemetryNames.LabelOperation, "create", RigMonitorTelemetryNames.LabelOutcome, "error"));
                    Assert.Equal(0D, capture.Sum(RigMonitorTelemetryNames.SamplesPersisted));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.Errors, RigMonitorTelemetryNames.LabelComponent, "sqlite"));
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// A successful collector job emits a root job span with queued, collect, and persist stage children,
        /// per-stage metrics, a job counter, and the last-success gauge; the retention run is also recorded.
        /// </summary>
        [Fact]
        public async Task CollectorJobShouldEmitJobAndStageTelemetry()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    PersistenceSettings settings = CreatePersistenceSettings(directory);
                    DatabaseDriverBase database = DatabaseDriverFactory.Create(settings);
                    await database.InitializeAsync(CancellationToken.None);
                    FakeTelemetryService telemetry = new FakeTelemetryService(() => BuildSnapshot(DateTime.UtcNow));
                    TelemetryPersistenceService service = new TelemetryPersistenceService(settings, database, telemetry, CreateLogger(directory));

                    await service.StartAsync(CancellationToken.None);
                    bool completed = await WaitUntilAsync(() => capture.Find(RigMonitorTelemetryNames.CollectorJobs).Any() && capture.Find(RigMonitorTelemetryNames.RetentionRuns).Any(), TimeSpan.FromSeconds(10));
                    await service.StopAsync();
                    service.Dispose();
                    database.Dispose();
                    Assert.True(completed);

                    Activity job = capture.Spans(RigMonitorTelemetryNames.SpanCollectorJob).First();
                    Assert.Equal(ActivityStatusCode.Ok, job.Status);
                    Assert.Equal(default(ActivitySpanId), job.ParentSpanId);
                    Assert.Equal("startup", job.GetTagItem("rigmonitor.collector.trigger"));

                    foreach (string stage in new[] { "queued", "collect", "persist" })
                    {
                        Activity stageSpan = capture.Spans("stage:" + stage).First(s => s.TraceId == job.TraceId);
                        Assert.Equal(job.SpanId, stageSpan.ParentSpanId);
                        Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorStageDuration, RigMonitorTelemetryNames.LabelStage, stage, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                        Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorStageEvents, RigMonitorTelemetryNames.LabelStage, stage, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                    }

                    Activity persist = capture.Spans("stage:persist").First(s => s.TraceId == job.TraceId);
                    Assert.Contains(capture.Spans("sqlite create"), s => s.ParentSpanId == persist.SpanId);

                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorJobs, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorJobDuration, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                    Assert.Equal(0D, capture.Sum(RigMonitorTelemetryNames.CollectorInProgress));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.RetentionRuns, RigMonitorTelemetryNames.LabelOutcome, "ok"));
                    Assert.NotEmpty(capture.Spans(RigMonitorTelemetryNames.SpanRetentionRun));

                    capture.CollectObservables();
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorLastSuccess));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.RetentionLastSuccess));
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// A collect-stage failure marks the stage and job as errors, names the failed stage, and counts the error.
        /// </summary>
        [Fact]
        public async Task CollectorCollectFailureShouldBeRecorded()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    PersistenceSettings settings = CreatePersistenceSettings(directory);
                    DatabaseDriverBase database = DatabaseDriverFactory.Create(settings);
                    await database.InitializeAsync(CancellationToken.None);
                    FakeTelemetryService telemetry = new FakeTelemetryService(() => throw new InvalidOperationException("collector exploded"));
                    TelemetryPersistenceService service = new TelemetryPersistenceService(settings, database, telemetry, CreateLogger(directory));

                    await service.StartAsync(CancellationToken.None);
                    bool completed = await WaitUntilAsync(() => capture.Find(RigMonitorTelemetryNames.CollectorJobs).Any(), TimeSpan.FromSeconds(10));
                    await service.StopAsync();
                    service.Dispose();
                    database.Dispose();
                    Assert.True(completed);

                    Activity job = capture.Spans(RigMonitorTelemetryNames.SpanCollectorJob).First();
                    Assert.Equal(ActivityStatusCode.Error, job.Status);
                    Assert.Equal("collect", job.GetTagItem("rigmonitor.failed_stage"));
                    Assert.Equal(ActivityStatusCode.Error, capture.Spans("stage:collect").First(s => s.TraceId == job.TraceId).Status);
                    Assert.DoesNotContain(capture.Spans("stage:persist"), s => s.TraceId == job.TraceId);

                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorJobs, RigMonitorTelemetryNames.LabelOutcome, "error"));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorStageEvents, RigMonitorTelemetryNames.LabelStage, "collect", RigMonitorTelemetryNames.LabelOutcome, "error"));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.Errors, RigMonitorTelemetryNames.LabelComponent, "collector.collect", RigMonitorTelemetryNames.LabelErrorType, typeof(InvalidOperationException).FullName!));
                    Assert.Equal(0D, capture.Sum(RigMonitorTelemetryNames.CollectorInProgress));
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// A persist-stage failure is attributed to the persist stage, and a failing retention run is recorded.
        /// </summary>
        [Fact]
        public async Task CollectorPersistAndRetentionFailuresShouldBeRecorded()
        {
            string directory = CreateTempDirectory();
            try
            {
                using (TelemetryCapture capture = new TelemetryCapture())
                {
                    PersistenceSettings settings = CreatePersistenceSettings(directory);

                    // Not initialized: inserts and prunes fail because the tables do not exist.
                    DatabaseDriverBase database = DatabaseDriverFactory.Create(settings);
                    FakeTelemetryService telemetry = new FakeTelemetryService(() => BuildSnapshot(DateTime.UtcNow));
                    TelemetryPersistenceService service = new TelemetryPersistenceService(settings, database, telemetry, CreateLogger(directory));

                    await service.StartAsync(CancellationToken.None);
                    bool completed = await WaitUntilAsync(() => capture.Find(RigMonitorTelemetryNames.CollectorJobs).Any() && capture.Find(RigMonitorTelemetryNames.RetentionRuns).Any(), TimeSpan.FromSeconds(10));
                    await service.StopAsync();
                    service.Dispose();
                    database.Dispose();
                    Assert.True(completed);

                    Activity job = capture.Spans(RigMonitorTelemetryNames.SpanCollectorJob).First();
                    Assert.Equal("persist", job.GetTagItem("rigmonitor.failed_stage"));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorStageEvents, RigMonitorTelemetryNames.LabelStage, "collect", RigMonitorTelemetryNames.LabelOutcome, "ok"));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.CollectorStageEvents, RigMonitorTelemetryNames.LabelStage, "persist", RigMonitorTelemetryNames.LabelOutcome, "error"));
                    Assert.NotEmpty(capture.Find(RigMonitorTelemetryNames.RetentionRuns, RigMonitorTelemetryNames.LabelOutcome, "error"));
                    Assert.Equal(ActivityStatusCode.Error, capture.Spans(RigMonitorTelemetryNames.SpanRetentionRun).First().Status);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static PersistenceSettings CreatePersistenceSettings(string directory)
        {
            return new PersistenceSettings
            {
                Enabled = true,
                Hostname = "observability-host",
                CollectionIntervalMs = 60000,
                RetentionDays = 1,
                PruneIntervalMinutes = 60,
                Database = new DatabaseSettings
                {
                    Filename = Path.Combine(directory, "history.db")
                }
            };
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

        private static TelemetrySnapshot BuildSnapshot(DateTime collectedUtc)
        {
            return new TelemetrySnapshot
            {
                CollectedUtc = collectedUtc,
                HostPlatform = HostPlatformEnum.Linux,
                Cpu = new CpuTelemetry { LogicalCoreCount = 4, UtilizationPercent = 10D },
                Collection = new TelemetryCollectionMetadata { CollectedUtc = collectedUtc }
            };
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(50);
            }

            return condition();
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
