namespace RigMonitor.Server.Services
{
    using Radiant;
    using RigMonitor.Core.Observability;

    /// <summary>
    /// Radiant metric catalog for RigMonitor's histograms. Registering it with the telemetry host applies
    /// explicit bucket boundaries in seconds so Grafana can derive p50/p95/p99 from histogram buckets.
    /// Thread safety: immutable after type initialization.
    /// </summary>
    public static class RigMonitorMetricCatalog
    {
        #region Public-Members

        /// <summary>
        /// Bucket boundaries in seconds spanning sub-millisecond database calls to minute-long timeouts.
        /// </summary>
        public static double[] WideBuckets
        {
            get
            {
                return new double[]
                {
                    0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0
                };
            }
        }

        /// <summary>
        /// Every catalogued RigMonitor and Watson histogram convention.
        /// </summary>
        public static Convention[] All
        {
            get
            {
                return new Convention[]
                {
                    Convention.Histogram("http.server.request.duration", "s", WideBuckets, "http.request.method", "http.response.status_code", "http.route", "url.scheme", "network.protocol.version", "error.type"),
                    Convention.Histogram(RigMonitorTelemetryNames.SnapshotDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelSnapshotScope, RigMonitorTelemetryNames.LabelOutcome),
                    Convention.Histogram(RigMonitorTelemetryNames.SectionDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelSection, RigMonitorTelemetryNames.LabelSectionStatus),
                    Convention.Histogram(RigMonitorTelemetryNames.CollectorJobDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelOutcome),
                    Convention.Histogram(RigMonitorTelemetryNames.CollectorStageDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelStage, RigMonitorTelemetryNames.LabelOutcome),
                    Convention.Histogram(RigMonitorTelemetryNames.RetentionDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelOutcome),
                    Convention.Histogram(RigMonitorTelemetryNames.IntegrationDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelService, RigMonitorTelemetryNames.LabelOperation, RigMonitorTelemetryNames.LabelOutcome),
                    Convention.Histogram(RigMonitorTelemetryNames.DatabaseLockWaitDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelOutcome),
                    Convention.Histogram(RigMonitorTelemetryNames.StartupDuration, "s", WideBuckets, RigMonitorTelemetryNames.LabelOutcome)
                };
            }
        }

        #endregion
    }
}
