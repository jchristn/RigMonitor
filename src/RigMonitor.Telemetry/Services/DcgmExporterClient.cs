namespace RigMonitor.Telemetry.Services
{
    using System;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Telemetry.Gpu.Prometheus;

    /// <summary>
    /// DCGM exporter HTTP client.
    /// </summary>
    public class DcgmExporterClient : PrometheusTextClient, IDcgmExporterClient
    {
        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public DcgmExporterClient(TelemetrySettings settings)
            : base(
                RigMonitorTelemetryNames.ServiceDcgm,
                "DCGM exporter",
                (settings ?? throw new ArgumentNullException(nameof(settings))).DcgmExporterUrl,
                settings.RequestTimeoutMs)
        {
        }
    }
}
