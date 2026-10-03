namespace RigMonitor.Core.Observability
{
    /// <summary>
    /// Stable public names for RigMonitor's own metrics, spans, and labels.
    /// These strings are a contract with collectors and Grafana dashboards; do not rename them casually.
    /// See TELEMETRY.md for the full catalog.
    /// </summary>
    public static class RigMonitorTelemetryNames
    {
        /// <summary>
        /// Name of the RigMonitor meter. Subscribe a collector to this name to receive application metrics.
        /// </summary>
        public const string MeterName = "RigMonitor";

        /// <summary>
        /// Name of the RigMonitor activity source. Subscribe a collector to this name to receive application spans.
        /// </summary>
        public const string ActivitySourceName = "RigMonitor";

        /// <summary>
        /// Name of the meter and activity source emitted by the Watson webserver.
        /// </summary>
        public const string WatsonSourceName = "Watson";

        /// <summary>
        /// Snapshot workflow duration histogram (seconds).
        /// </summary>
        public const string SnapshotDuration = "rigmonitor.snapshot.duration";

        /// <summary>
        /// Snapshot workflow counter by scope and outcome.
        /// </summary>
        public const string SnapshotCount = "rigmonitor.snapshot.count";

        /// <summary>
        /// Per-section collection duration histogram (seconds).
        /// </summary>
        public const string SectionDuration = "rigmonitor.section.duration";

        /// <summary>
        /// Per-section collection counter by section and status.
        /// </summary>
        public const string SectionCollections = "rigmonitor.section.collections";

        /// <summary>
        /// Per-section last successful collection gauge (Unix seconds).
        /// </summary>
        public const string SectionLastSuccess = "rigmonitor.section.last_success";

        /// <summary>
        /// Background collector job counter by outcome.
        /// </summary>
        public const string CollectorJobs = "rigmonitor.collector.jobs";

        /// <summary>
        /// Background collector job duration histogram (seconds).
        /// </summary>
        public const string CollectorJobDuration = "rigmonitor.collector.job.duration";

        /// <summary>
        /// Background collector per-stage duration histogram (seconds).
        /// </summary>
        public const string CollectorStageDuration = "rigmonitor.collector.stage.duration";

        /// <summary>
        /// Background collector per-stage event counter by stage and outcome.
        /// </summary>
        public const string CollectorStageEvents = "rigmonitor.collector.stage.events";

        /// <summary>
        /// Background collector last successful job gauge (Unix seconds).
        /// </summary>
        public const string CollectorLastSuccess = "rigmonitor.collector.last_success";

        /// <summary>
        /// Background collector concurrency slot in use (0 or 1).
        /// </summary>
        public const string CollectorInProgress = "rigmonitor.collector.in_progress";

        /// <summary>
        /// Retention prune run counter by outcome.
        /// </summary>
        public const string RetentionRuns = "rigmonitor.retention.runs";

        /// <summary>
        /// Retention prune run duration histogram (seconds).
        /// </summary>
        public const string RetentionDuration = "rigmonitor.retention.duration";

        /// <summary>
        /// Retention prune last successful run gauge (Unix seconds).
        /// </summary>
        public const string RetentionLastSuccess = "rigmonitor.retention.last_success";

        /// <summary>
        /// Persisted telemetry sample counter.
        /// </summary>
        public const string SamplesPersisted = "rigmonitor.samples.persisted";

        /// <summary>
        /// Deleted telemetry sample counter by reason.
        /// </summary>
        public const string SamplesDeleted = "rigmonitor.samples.deleted";

        /// <summary>
        /// Outbound integration call counter by service, operation, and outcome.
        /// </summary>
        public const string IntegrationRequests = "rigmonitor.integration.requests";

        /// <summary>
        /// Outbound integration call duration histogram (seconds).
        /// </summary>
        public const string IntegrationDuration = "rigmonitor.integration.duration";

        /// <summary>
        /// Time spent waiting for the database concurrency slot, histogram (seconds).
        /// </summary>
        public const string DatabaseLockWaitDuration = "rigmonitor.db.lock.wait_duration";

        /// <summary>
        /// Callers currently waiting for the database concurrency slot.
        /// </summary>
        public const string DatabaseLockWaiting = "rigmonitor.db.lock.waiting";

        /// <summary>
        /// Database concurrency slots currently held.
        /// </summary>
        public const string DatabaseLockInUse = "rigmonitor.db.lock.in_use";

        /// <summary>
        /// Database concurrency slot capacity gauge.
        /// </summary>
        public const string DatabaseLockCapacity = "rigmonitor.db.lock.capacity";

        /// <summary>
        /// Error counter by component and exception type.
        /// </summary>
        public const string Errors = "rigmonitor.errors";

        /// <summary>
        /// Utilyze live WebSocket connection state gauge (0 or 1).
        /// </summary>
        public const string UtilyzeConnected = "rigmonitor.utilyze.connected";

        /// <summary>
        /// Utilyze live WebSocket message counter by event type.
        /// </summary>
        public const string UtilyzeMessages = "rigmonitor.utilyze.messages";

        /// <summary>
        /// Runtime capability availability gauge (0 or 1) by capability.
        /// </summary>
        public const string CapabilityAvailable = "rigmonitor.capability.available";

        /// <summary>
        /// Telemetry sampler warm state gauge (0 or 1).
        /// </summary>
        public const string TelemetryWarm = "rigmonitor.telemetry.warm";

        /// <summary>
        /// Build information gauge (always 1) labeled with version and runtime.
        /// </summary>
        public const string BuildInfo = "rigmonitor.build.info";

        /// <summary>
        /// Startup duration histogram (seconds).
        /// </summary>
        public const string StartupDuration = "rigmonitor.startup.duration";

        /// <summary>
        /// Configured background collection interval gauge (seconds).
        /// </summary>
        public const string ConfigCollectionInterval = "rigmonitor.config.collection_interval";

        /// <summary>
        /// Configured retention window gauge (seconds).
        /// </summary>
        public const string ConfigRetention = "rigmonitor.config.retention";

        /// <summary>
        /// Configured outbound request timeout gauge (seconds).
        /// </summary>
        public const string ConfigRequestTimeout = "rigmonitor.config.request_timeout";

        /// <summary>
        /// Configured section stale window gauge (seconds).
        /// </summary>
        public const string ConfigSectionStaleAfter = "rigmonitor.config.section_stale_after";

        /// <summary>
        /// Configured persistence enabled flag gauge (0 or 1).
        /// </summary>
        public const string ConfigPersistenceEnabled = "rigmonitor.config.persistence_enabled";

        /// <summary>
        /// Label: outcome of an operation (ok, error, timeout, cancelled, http_error, unavailable, skipped, degraded).
        /// </summary>
        public const string LabelOutcome = "outcome";

        /// <summary>
        /// Label: snapshot scope (full or partial).
        /// </summary>
        public const string LabelSnapshotScope = "rigmonitor.snapshot.scope";

        /// <summary>
        /// Label: telemetry section name.
        /// </summary>
        public const string LabelSection = "rigmonitor.section";

        /// <summary>
        /// Label: telemetry section collection status.
        /// </summary>
        public const string LabelSectionStatus = "rigmonitor.section.status";

        /// <summary>
        /// Label: background pipeline stage name.
        /// </summary>
        public const string LabelStage = "rigmonitor.stage";

        /// <summary>
        /// Label: outbound integration service name.
        /// </summary>
        public const string LabelService = "rigmonitor.integration.service";

        /// <summary>
        /// Label: outbound integration operation name.
        /// </summary>
        public const string LabelOperation = "rigmonitor.integration.operation";

        /// <summary>
        /// Label: component that raised an error.
        /// </summary>
        public const string LabelComponent = "rigmonitor.component";

        /// <summary>
        /// Label: OpenTelemetry error type (exception type name).
        /// </summary>
        public const string LabelErrorType = "error.type";

        /// <summary>
        /// Label: sample deletion reason (retention, api, api_bulk).
        /// </summary>
        public const string LabelDeleteReason = "rigmonitor.delete.reason";

        /// <summary>
        /// Label: Utilyze event type (init, metrics, ceilings, unknown).
        /// </summary>
        public const string LabelUtilyzeEvent = "rigmonitor.utilyze.event";

        /// <summary>
        /// Label: runtime capability name (nvidia, ollama, vllm, utilyze).
        /// </summary>
        public const string LabelCapability = "rigmonitor.capability";

        /// <summary>
        /// Label: RigMonitor version.
        /// </summary>
        public const string LabelVersion = "rigmonitor.version";

        /// <summary>
        /// Label: .NET runtime version.
        /// </summary>
        public const string LabelRuntime = "process.runtime.version";

        /// <summary>
        /// Span: telemetry snapshot workflow root.
        /// </summary>
        public const string SpanSnapshot = "telemetry.snapshot";

        /// <summary>
        /// Span name prefix for a stage or section (for example stage:cpu, stage:persist).
        /// </summary>
        public const string SpanStagePrefix = "stage:";

        /// <summary>
        /// Span: background collector job root.
        /// </summary>
        public const string SpanCollectorJob = "collector.job";

        /// <summary>
        /// Span: retention prune run root.
        /// </summary>
        public const string SpanRetentionRun = "retention.run";

        /// <summary>
        /// Span: process startup root.
        /// </summary>
        public const string SpanStartup = "rigmonitor.startup";

        /// <summary>
        /// Span: runtime capability probing.
        /// </summary>
        public const string SpanCapabilitiesProbe = "capabilities.probe";

        /// <summary>
        /// Span: telemetry sampler warmup.
        /// </summary>
        public const string SpanWarmup = "telemetry.warmup";

        /// <summary>
        /// Outcome value: success.
        /// </summary>
        public const string OutcomeOk = "ok";

        /// <summary>
        /// Outcome value: failure.
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Outcome value: timed out.
        /// </summary>
        public const string OutcomeTimeout = "timeout";

        /// <summary>
        /// Outcome value: cancelled by the caller or shutdown.
        /// </summary>
        public const string OutcomeCancelled = "cancelled";

        /// <summary>
        /// Outcome value: remote returned a non-success HTTP status.
        /// </summary>
        public const string OutcomeHttpError = "http_error";

        /// <summary>
        /// Outcome value: dependency returned no usable data.
        /// </summary>
        public const string OutcomeUnavailable = "unavailable";

        /// <summary>
        /// Outcome value: work was skipped (for example the concurrency slot was busy).
        /// </summary>
        public const string OutcomeSkipped = "skipped";

        /// <summary>
        /// Outcome value: completed, but one or more requested sections did not return data.
        /// </summary>
        public const string OutcomeDegraded = "degraded";

        /// <summary>
        /// Integration service: NVIDIA DCGM exporter.
        /// </summary>
        public const string ServiceDcgm = "dcgm";

        /// <summary>
        /// Integration service: Ollama API.
        /// </summary>
        public const string ServiceOllama = "ollama";

        /// <summary>
        /// Integration service: vLLM metrics endpoint.
        /// </summary>
        public const string ServiceVllm = "vllm";

        /// <summary>
        /// Integration service: Utilyze sidecar.
        /// </summary>
        public const string ServiceUtilyze = "utilyze";

        /// <summary>
        /// Integration service: SQLite telemetry history database.
        /// </summary>
        public const string ServiceSqlite = "sqlite";

        /// <summary>
        /// Integration service: child process execution.
        /// </summary>
        public const string ServiceProcess = "process";

        /// <summary>
        /// Collector stage: wait between the scheduled tick and the start of work.
        /// </summary>
        public const string StageQueued = "queued";

        /// <summary>
        /// Collector stage: telemetry snapshot collection.
        /// </summary>
        public const string StageCollect = "collect";

        /// <summary>
        /// Collector stage: database persistence.
        /// </summary>
        public const string StagePersist = "persist";
    }
}
