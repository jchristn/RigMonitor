namespace RigMonitor.Core.Observability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// RigMonitor's application-level meter, activity source, and typed recording helpers.
    /// Emission rides the .NET base class library only: with no listener attached every call is a cheap no-op.
    /// Every member is best-effort and never throws into the caller.
    /// Thread safety: all members are safe for concurrent use.
    /// </summary>
    public static class RigMonitorInstrumentation
    {
        #region Public-Members

        /// <summary>
        /// The RigMonitor meter, named <see cref="RigMonitorTelemetryNames.MeterName"/>.
        /// </summary>
        public static Meter Meter
        {
            get
            {
                return _Meter;
            }
        }

        /// <summary>
        /// The RigMonitor activity source, named <see cref="RigMonitorTelemetryNames.ActivitySourceName"/>.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get
            {
                return _ActivitySource;
            }
        }

        /// <summary>
        /// RigMonitor version reported on the build-info gauge, without source revision metadata.
        /// </summary>
        public static string Version
        {
            get
            {
                return _Version;
            }
        }

        #endregion

        #region Private-Members

        private static readonly string _Version = ResolveVersion();
        private static readonly Meter _Meter = new Meter(RigMonitorTelemetryNames.MeterName, _Version);
        private static readonly ActivitySource _ActivitySource = new ActivitySource(RigMonitorTelemetryNames.ActivitySourceName, _Version);

        private static readonly Histogram<double> _SnapshotDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.SnapshotDuration, "s", "Duration of a full or partial telemetry snapshot.");
        private static readonly Counter<long> _SnapshotCount = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.SnapshotCount, "{snapshot}", "Telemetry snapshots by scope and outcome.");
        private static readonly Histogram<double> _SectionDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.SectionDuration, "s", "Duration of one telemetry section collection.");
        private static readonly Counter<long> _SectionCollections = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.SectionCollections, "{collection}", "Telemetry section collections by section and status.");
        private static readonly Counter<long> _CollectorJobs = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.CollectorJobs, "{job}", "Background collector jobs by outcome.");
        private static readonly Histogram<double> _CollectorJobDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.CollectorJobDuration, "s", "Duration of one background collector job.");
        private static readonly Histogram<double> _CollectorStageDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.CollectorStageDuration, "s", "Duration of one background collector stage.");
        private static readonly Counter<long> _CollectorStageEvents = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.CollectorStageEvents, "{event}", "Background collector stage events by stage and outcome.");
        private static readonly UpDownCounter<long> _CollectorInProgress = _Meter.CreateUpDownCounter<long>(
            RigMonitorTelemetryNames.CollectorInProgress, "{job}", "Background collector concurrency slot in use (capacity 1).");
        private static readonly Counter<long> _RetentionRuns = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.RetentionRuns, "{run}", "Retention prune runs by outcome.");
        private static readonly Histogram<double> _RetentionDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.RetentionDuration, "s", "Duration of one retention prune run.");
        private static readonly Counter<long> _SamplesPersisted = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.SamplesPersisted, "{sample}", "Telemetry samples written to the history database.");
        private static readonly Counter<long> _SamplesDeleted = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.SamplesDeleted, "{sample}", "Telemetry samples deleted from the history database by reason.");
        private static readonly Counter<long> _IntegrationRequests = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.IntegrationRequests, "{request}", "Outbound integration calls by service, operation, and outcome.");
        private static readonly Histogram<double> _IntegrationDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.IntegrationDuration, "s", "Duration of one outbound integration call.");
        private static readonly Histogram<double> _DatabaseLockWaitDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.DatabaseLockWaitDuration, "s", "Time spent waiting for the database concurrency slot.");
        private static readonly UpDownCounter<long> _DatabaseLockWaiting = _Meter.CreateUpDownCounter<long>(
            RigMonitorTelemetryNames.DatabaseLockWaiting, "{caller}", "Callers waiting for the database concurrency slot.");
        private static readonly UpDownCounter<long> _DatabaseLockInUse = _Meter.CreateUpDownCounter<long>(
            RigMonitorTelemetryNames.DatabaseLockInUse, "{slot}", "Database concurrency slots currently held.");
        private static readonly Counter<long> _Errors = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.Errors, "{error}", "Errors by component and exception type.");
        private static readonly Counter<long> _UtilyzeMessages = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.UtilyzeMessages, "{message}", "Utilyze live WebSocket messages by event type.");
        private static readonly Histogram<double> _StartupDuration = _Meter.CreateHistogram<double>(
            RigMonitorTelemetryNames.StartupDuration, "s", "Duration of process startup initialization.");

        private static readonly ConcurrentDictionary<string, double> _SectionLastSuccess = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, int> _Capabilities = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, int> _GpuNativeFeatures = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        private static readonly Counter<long> _GpuNativeErrors = _Meter.CreateCounter<long>(
            RigMonitorTelemetryNames.GpuNativeErrors, "{error}", "Native GPU collection errors by vendor and feature.");
        private static readonly object _ConfigurationLock = new object();
        private static double _CollectorLastSuccess = 0D;
        private static double _RetentionLastSuccess = 0D;
        private static int _UtilyzeConnected = -1;
        private static int _TelemetryWarm = -1;
        private static double? _ConfigCollectionIntervalSeconds = null;
        private static double? _ConfigRetentionSeconds = null;
        private static double? _ConfigRequestTimeoutSeconds = null;
        private static double? _ConfigSectionStaleSeconds = null;
        private static int? _ConfigPersistenceEnabled = null;

        #endregion

        #region Constructors-and-Factories

        static RigMonitorInstrumentation()
        {
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.SectionLastSuccess, ObserveSectionLastSuccess, "s", "Unix time of the last successful collection per section.");
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.CollectorLastSuccess, ObserveCollectorLastSuccess, "s", "Unix time of the last successful background collector job.");
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.RetentionLastSuccess, ObserveRetentionLastSuccess, "s", "Unix time of the last successful retention prune run.");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.UtilyzeConnected, ObserveUtilyzeConnected, "1", "Whether the Utilyze live WebSocket is connected (1) or not (0).");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.CapabilityAvailable, ObserveCapabilities, "1", "Whether a runtime capability was detected at startup (1) or not (0).");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.GpuNativeFeatureAvailable, ObserveGpuNativeFeatures, "1", "Whether a native GPU feature loaded (1) or not (0).");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.TelemetryWarm, ObserveTelemetryWarm, "1", "Whether telemetry samplers are warm (1) or not (0).");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.DatabaseLockCapacity, () => 1, "{slot}", "Database concurrency slot capacity (SQLite access is serialized).");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.BuildInfo, ObserveBuildInfo, "1", "Build information; always 1, labeled with version and runtime.");
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.ConfigCollectionInterval, () => ObserveConfiguration(ReadConfiguration(ref _ConfigCollectionIntervalSeconds)), "s", "Configured background collection interval.");
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.ConfigRetention, () => ObserveConfiguration(ReadConfiguration(ref _ConfigRetentionSeconds)), "s", "Configured telemetry history retention window.");
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.ConfigRequestTimeout, () => ObserveConfiguration(ReadConfiguration(ref _ConfigRequestTimeoutSeconds)), "s", "Configured outbound request timeout.");
            _Meter.CreateObservableGauge<double>(
                RigMonitorTelemetryNames.ConfigSectionStaleAfter, () => ObserveConfiguration(ReadConfiguration(ref _ConfigSectionStaleSeconds)), "s", "Configured section freshness window.");
            _Meter.CreateObservableGauge<int>(
                RigMonitorTelemetryNames.ConfigPersistenceEnabled, ObservePersistenceEnabled, "1", "Whether background persistence is enabled (1) or not (0).");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a span on the RigMonitor activity source. Returns null when nothing is listening or creation fails,
        /// so callers use the null-conditional operator.
        /// </summary>
        /// <param name="name">Span name. Use a stable, low-cardinality name.</param>
        /// <param name="kind">Span kind. Default is <see cref="ActivityKind.Internal"/>.</param>
        /// <returns>The started activity, or null.</returns>
        public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            try
            {
                return _ActivitySource.StartActivity(name, kind);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Start a span with an explicit start time, used to record a wait that has already elapsed.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="startTime">Start time of the span.</param>
        /// <returns>The started activity, or null.</returns>
        public static Activity? StartActivityAt(string name, DateTimeOffset startTime)
        {
            try
            {
                ActivityContext parent = Activity.Current != null ? Activity.Current.Context : default(ActivityContext);
                return _ActivitySource.StartActivity(name, ActivityKind.Internal, parent, null, null, startTime);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Start a new root span (no parent), optionally linked to the span that triggered the work.
        /// Use for long-lived background work handed off from a request so it does not inflate the request trace.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="link">Optional context of the triggering span, recorded as a span link.</param>
        /// <returns>The started activity, or null.</returns>
        public static Activity? StartRootActivity(string name, ActivityKind kind, ActivityContext? link)
        {
            try
            {
                Activity.Current = null;
                List<ActivityLink>? links = null;
                if (link.HasValue && link.Value != default(ActivityContext))
                {
                    links = new List<ActivityLink> { new ActivityLink(link.Value) };
                }

                return _ActivitySource.StartActivity(name, kind, default(ActivityContext), null, links);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Mark a span successful.
        /// </summary>
        /// <param name="activity">Span, may be null.</param>
        public static void SetOk(Activity? activity)
        {
            if (activity == null) return;
            try
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Mark a span failed with a message and no exception.
        /// </summary>
        /// <param name="activity">Span, may be null.</param>
        /// <param name="errorType">Low-cardinality error type for the error.type attribute.</param>
        /// <param name="description">Status description. Must not contain secrets or payloads.</param>
        public static void SetError(Activity? activity, string errorType, string? description)
        {
            if (activity == null) return;
            try
            {
                activity.SetTag(RigMonitorTelemetryNames.LabelErrorType, errorType);
                activity.SetStatus(ActivityStatusCode.Error, description);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Mark a span failed and record the exception as a span event.
        /// </summary>
        /// <param name="activity">Span, may be null.</param>
        /// <param name="exception">Exception, may be null.</param>
        public static void SetError(Activity? activity, Exception? exception)
        {
            if (activity == null) return;
            try
            {
                if (exception == null)
                {
                    activity.SetStatus(ActivityStatusCode.Error);
                    return;
                }

                activity.SetTag(RigMonitorTelemetryNames.LabelErrorType, GetErrorType(exception));
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.AddException(exception);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Classify an exception into a bounded outcome value.
        /// </summary>
        /// <param name="exception">Exception, may be null.</param>
        /// <param name="cancellationToken">Caller token, used to tell timeouts from cancellation.</param>
        /// <returns>One of the outcome constants in <see cref="RigMonitorTelemetryNames"/>.</returns>
        public static string ClassifyException(Exception? exception, CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException)
            {
                return cancellationToken.IsCancellationRequested
                    ? RigMonitorTelemetryNames.OutcomeCancelled
                    : RigMonitorTelemetryNames.OutcomeTimeout;
            }

            if (exception is TimeoutException) return RigMonitorTelemetryNames.OutcomeTimeout;
            return RigMonitorTelemetryNames.OutcomeError;
        }

        /// <summary>
        /// Get the bounded error.type value for an exception (its type name).
        /// </summary>
        /// <param name="exception">Exception, may be null.</param>
        /// <returns>Exception type name, or "unknown".</returns>
        public static string GetErrorType(Exception? exception)
        {
            if (exception == null) return "unknown";
            return exception.GetType().FullName ?? exception.GetType().Name;
        }

        /// <summary>
        /// Record a completed telemetry snapshot.
        /// </summary>
        /// <param name="scope">full or partial.</param>
        /// <param name="outcome">ok, degraded, cancelled, or error.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordSnapshot(string scope, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { RigMonitorTelemetryNames.LabelSnapshotScope, scope },
                    { RigMonitorTelemetryNames.LabelOutcome, outcome }
                };
                _SnapshotCount.Add(1, tags);
                _SnapshotDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record one telemetry section collection.
        /// </summary>
        /// <param name="section">Section name.</param>
        /// <param name="status">Collection status (ok, unavailable, error, timeout, unsupported, disabled).</param>
        /// <param name="seconds">Duration in seconds, or null when no collection was attempted.</param>
        public static void RecordSection(string section, string status, double? seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { RigMonitorTelemetryNames.LabelSection, section },
                    { RigMonitorTelemetryNames.LabelSectionStatus, status }
                };
                _SectionCollections.Add(1, tags);
                if (seconds.HasValue) _SectionDuration.Record(seconds.Value, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record the last successful collection time for a section.
        /// </summary>
        /// <param name="section">Section name.</param>
        /// <param name="successUtc">Success time in UTC.</param>
        public static void SetSectionLastSuccess(string section, DateTime successUtc)
        {
            try
            {
                _SectionLastSuccess[section] = ToUnixSeconds(successUtc);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a completed background collector job.
        /// </summary>
        /// <param name="outcome">ok, error, skipped, or cancelled.</param>
        /// <param name="seconds">Duration in seconds, or null for a skipped job.</param>
        public static void RecordCollectorJob(string outcome, double? seconds)
        {
            try
            {
                TagList tags = new TagList { { RigMonitorTelemetryNames.LabelOutcome, outcome } };
                _CollectorJobs.Add(1, tags);
                if (seconds.HasValue) _CollectorJobDuration.Record(seconds.Value, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a completed background collector stage.
        /// </summary>
        /// <param name="stage">queued, collect, or persist.</param>
        /// <param name="outcome">Stage outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordCollectorStage(string stage, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { RigMonitorTelemetryNames.LabelStage, stage },
                    { RigMonitorTelemetryNames.LabelOutcome, outcome }
                };
                _CollectorStageEvents.Add(1, tags);
                _CollectorStageDuration.Record(Math.Max(0D, seconds), tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record the last successful background collector job time.
        /// </summary>
        /// <param name="successUtc">Success time in UTC.</param>
        public static void SetCollectorLastSuccess(DateTime successUtc)
        {
            Interlocked.Exchange(ref _CollectorLastSuccess, ToUnixSeconds(successUtc));
        }

        /// <summary>
        /// Adjust the background collector in-progress slot counter.
        /// </summary>
        /// <param name="delta">+1 when a job starts, -1 when it ends.</param>
        public static void AddCollectorInProgress(long delta)
        {
            try
            {
                _CollectorInProgress.Add(delta);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a completed retention prune run.
        /// </summary>
        /// <param name="outcome">ok, error, or cancelled.</param>
        /// <param name="seconds">Duration in seconds.</param>
        /// <param name="completedUtc">Completion time in UTC; sets the last-success gauge when outcome is ok.</param>
        public static void RecordRetentionRun(string outcome, double seconds, DateTime completedUtc)
        {
            try
            {
                TagList tags = new TagList { { RigMonitorTelemetryNames.LabelOutcome, outcome } };
                _RetentionRuns.Add(1, tags);
                _RetentionDuration.Record(seconds, tags);
                if (outcome == RigMonitorTelemetryNames.OutcomeOk)
                {
                    Interlocked.Exchange(ref _RetentionLastSuccess, ToUnixSeconds(completedUtc));
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record persisted telemetry samples.
        /// </summary>
        /// <param name="count">Sample count.</param>
        public static void RecordSamplesPersisted(long count)
        {
            if (count <= 0L) return;
            try
            {
                _SamplesPersisted.Add(count);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record deleted telemetry samples.
        /// </summary>
        /// <param name="reason">retention, api, or api_bulk.</param>
        /// <param name="count">Sample count.</param>
        public static void RecordSamplesDeleted(string reason, long count)
        {
            if (count <= 0L) return;
            try
            {
                _SamplesDeleted.Add(count, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelDeleteReason, reason));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a completed outbound integration call.
        /// </summary>
        /// <param name="service">Integration service name.</param>
        /// <param name="operation">Bounded operation name.</param>
        /// <param name="outcome">Call outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordIntegration(string service, string operation, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { RigMonitorTelemetryNames.LabelService, service },
                    { RigMonitorTelemetryNames.LabelOperation, operation },
                    { RigMonitorTelemetryNames.LabelOutcome, outcome }
                };
                _IntegrationRequests.Add(1, tags);
                _IntegrationDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a completed wait for the database concurrency slot.
        /// </summary>
        /// <param name="outcome">ok, timeout, or cancelled.</param>
        /// <param name="seconds">Wait duration in seconds.</param>
        public static void RecordDatabaseLockWait(string outcome, double seconds)
        {
            try
            {
                _DatabaseLockWaitDuration.Record(seconds, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelOutcome, outcome));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Adjust the count of callers waiting for the database concurrency slot.
        /// </summary>
        /// <param name="delta">+1 when a wait starts, -1 when it ends.</param>
        public static void AddDatabaseLockWaiting(long delta)
        {
            try
            {
                _DatabaseLockWaiting.Add(delta);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Adjust the count of held database concurrency slots.
        /// </summary>
        /// <param name="delta">+1 on acquire, -1 on release.</param>
        public static void AddDatabaseLockInUse(long delta)
        {
            try
            {
                _DatabaseLockInUse.Add(delta);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record an error by component and exception type.
        /// </summary>
        /// <param name="component">Bounded component name (for example collector, retention, section, dcgm).</param>
        /// <param name="exception">Exception, may be null.</param>
        public static void RecordError(string component, Exception? exception)
        {
            RecordError(component, GetErrorType(exception));
        }

        /// <summary>
        /// Record an error by component and a bounded error type.
        /// </summary>
        /// <param name="component">Bounded component name.</param>
        /// <param name="errorType">Bounded error type.</param>
        public static void RecordError(string component, string errorType)
        {
            try
            {
                TagList tags = new TagList
                {
                    { RigMonitorTelemetryNames.LabelComponent, component },
                    { RigMonitorTelemetryNames.LabelErrorType, errorType }
                };
                _Errors.Add(1, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a Utilyze live WebSocket message.
        /// </summary>
        /// <param name="eventType">init, metrics, ceilings, or unknown.</param>
        public static void RecordUtilyzeMessage(string eventType)
        {
            try
            {
                _UtilyzeMessages.Add(1, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelUtilyzeEvent, eventType));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set the Utilyze live WebSocket connection state.
        /// </summary>
        /// <param name="connected">True when connected.</param>
        public static void SetUtilyzeConnected(bool connected)
        {
            Interlocked.Exchange(ref _UtilyzeConnected, connected ? 1 : 0);
        }

        /// <summary>
        /// Set a runtime capability availability flag.
        /// </summary>
        /// <param name="capability">nvidia, ollama, vllm, or utilyze.</param>
        /// <param name="available">True when available.</param>
        public static void SetCapability(string capability, bool available)
        {
            try
            {
                _Capabilities[capability] = available ? 1 : 0;
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set a native GPU feature availability flag.
        /// </summary>
        /// <param name="feature">Feature name from the fixed feature set.</param>
        /// <param name="available">True when the feature loaded.</param>
        public static void SetGpuNativeFeature(string feature, bool available)
        {
            try
            {
                _GpuNativeFeatures[feature] = available ? 1 : 0;
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a native GPU collection error.
        /// </summary>
        /// <param name="vendor">Vendor (nvidia, amd, apple, qualcomm).</param>
        /// <param name="feature">Feature name from the fixed feature set.</param>
        public static void RecordGpuNativeError(string vendor, string feature)
        {
            try
            {
                _GpuNativeErrors.Add(
                    1,
                    new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelGpuVendor, vendor),
                    new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelGpuFeature, feature));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set the telemetry warm state.
        /// </summary>
        /// <param name="warm">True when warm.</param>
        public static void SetTelemetryWarm(bool warm)
        {
            Interlocked.Exchange(ref _TelemetryWarm, warm ? 1 : 0);
        }

        /// <summary>
        /// Record process startup duration.
        /// </summary>
        /// <param name="outcome">ok or error.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordStartup(string outcome, double seconds)
        {
            try
            {
                _StartupDuration.Record(seconds, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelOutcome, outcome));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Publish safe configuration values as gauges. Never pass secrets or URLs.
        /// </summary>
        /// <param name="collectionIntervalSeconds">Background collection interval in seconds.</param>
        /// <param name="retentionSeconds">Retention window in seconds.</param>
        /// <param name="requestTimeoutSeconds">Outbound request timeout in seconds.</param>
        /// <param name="sectionStaleSeconds">Section freshness window in seconds.</param>
        /// <param name="persistenceEnabled">Whether persistence is enabled.</param>
        public static void SetConfiguration(
            double collectionIntervalSeconds,
            double retentionSeconds,
            double requestTimeoutSeconds,
            double sectionStaleSeconds,
            bool persistenceEnabled)
        {
            lock (_ConfigurationLock)
            {
                _ConfigCollectionIntervalSeconds = collectionIntervalSeconds;
                _ConfigRetentionSeconds = retentionSeconds;
                _ConfigRequestTimeoutSeconds = requestTimeoutSeconds;
                _ConfigSectionStaleSeconds = sectionStaleSeconds;
                _ConfigPersistenceEnabled = persistenceEnabled ? 1 : 0;
            }
        }

        /// <summary>
        /// Elapsed seconds since a <see cref="Stopwatch.GetTimestamp"/> value.
        /// </summary>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Elapsed seconds.</returns>
        public static double ElapsedSeconds(long startTimestamp)
        {
            return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        }

        #endregion

        #region Private-Methods

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(RigMonitorInstrumentation).Assembly;
                AssemblyInformationalVersionAttribute? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = informational?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
                int plus = version.IndexOf('+');
                return plus > 0 ? version.Substring(0, plus) : version;
            }
            catch (Exception)
            {
                return "0.0.0";
            }
        }

        private static double ToUnixSeconds(DateTime utc)
        {
            DateTime normalized = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return new DateTimeOffset(normalized).ToUnixTimeMilliseconds() / 1000D;
        }

        private static IEnumerable<Measurement<double>> ObserveSectionLastSuccess()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            foreach (KeyValuePair<string, double> pair in _SectionLastSuccess)
            {
                measurements.Add(new Measurement<double>(pair.Value, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelSection, pair.Key)));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<double>> ObserveCollectorLastSuccess()
        {
            double value = Interlocked.CompareExchange(ref _CollectorLastSuccess, 0D, 0D);
            if (value <= 0D) return Array.Empty<Measurement<double>>();
            return new Measurement<double>[] { new Measurement<double>(value) };
        }

        private static IEnumerable<Measurement<double>> ObserveRetentionLastSuccess()
        {
            double value = Interlocked.CompareExchange(ref _RetentionLastSuccess, 0D, 0D);
            if (value <= 0D) return Array.Empty<Measurement<double>>();
            return new Measurement<double>[] { new Measurement<double>(value) };
        }

        private static IEnumerable<Measurement<int>> ObserveUtilyzeConnected()
        {
            int value = Volatile.Read(ref _UtilyzeConnected);
            if (value < 0) return Array.Empty<Measurement<int>>();
            return new Measurement<int>[] { new Measurement<int>(value) };
        }

        private static IEnumerable<Measurement<int>> ObserveTelemetryWarm()
        {
            int value = Volatile.Read(ref _TelemetryWarm);
            if (value < 0) return Array.Empty<Measurement<int>>();
            return new Measurement<int>[] { new Measurement<int>(value) };
        }

        private static IEnumerable<Measurement<int>> ObserveGpuNativeFeatures()
        {
            List<Measurement<int>> measurements = new List<Measurement<int>>();
            foreach (KeyValuePair<string, int> pair in _GpuNativeFeatures)
            {
                measurements.Add(new Measurement<int>(pair.Value, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelGpuFeature, pair.Key)));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<int>> ObserveCapabilities()
        {
            List<Measurement<int>> measurements = new List<Measurement<int>>();
            foreach (KeyValuePair<string, int> pair in _Capabilities)
            {
                measurements.Add(new Measurement<int>(pair.Value, new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelCapability, pair.Key)));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<int>> ObserveBuildInfo()
        {
            return new Measurement<int>[]
            {
                new Measurement<int>(
                    1,
                    new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelVersion, _Version),
                    new KeyValuePair<string, object?>(RigMonitorTelemetryNames.LabelRuntime, Environment.Version.ToString()))
            };
        }

        private static double? ReadConfiguration(ref double? field)
        {
            lock (_ConfigurationLock)
            {
                return field;
            }
        }

        private static IEnumerable<Measurement<double>> ObserveConfiguration(double? value)
        {
            if (!value.HasValue) return Array.Empty<Measurement<double>>();
            return new Measurement<double>[] { new Measurement<double>(value.Value) };
        }

        private static IEnumerable<Measurement<int>> ObservePersistenceEnabled()
        {
            int? value;
            lock (_ConfigurationLock)
            {
                value = _ConfigPersistenceEnabled;
            }

            if (!value.HasValue) return Array.Empty<Measurement<int>>();
            return new Measurement<int>[] { new Measurement<int>(value.Value) };
        }

        #endregion
    }
}
