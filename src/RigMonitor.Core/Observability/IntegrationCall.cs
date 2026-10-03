namespace RigMonitor.Core.Observability
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// Instruments one outbound integration call: a client span named "service operation", plus the
    /// integration request counter and duration histogram. Dispose records the result exactly once; a call
    /// disposed without <see cref="Succeed"/> or a failure method is recorded as an error.
    /// Best-effort: never throws. Not thread safe; use one instance per call.
    /// </summary>
    public sealed class IntegrationCall : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// The client span, or null when nothing is listening.
        /// </summary>
        public Activity? Activity
        {
            get
            {
                return _Activity;
            }
        }

        /// <summary>
        /// Integration service name.
        /// </summary>
        public string Service
        {
            get
            {
                return _Service;
            }
        }

        /// <summary>
        /// Integration operation name.
        /// </summary>
        public string Operation
        {
            get
            {
                return _Operation;
            }
        }

        /// <summary>
        /// Outcome recorded so far, or null when the call has not completed.
        /// </summary>
        public string? Outcome
        {
            get
            {
                return _Outcome;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Service;
        private readonly string _Operation;
        private readonly Activity? _Activity;
        private readonly long _StartTimestamp;
        private string? _Outcome = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        private IntegrationCall(string service, string operation, Uri? target, ActivityKind kind)
        {
            _Service = String.IsNullOrWhiteSpace(service) ? "unknown" : service;
            _Operation = String.IsNullOrWhiteSpace(operation) ? "unknown" : operation;
            _StartTimestamp = Stopwatch.GetTimestamp();
            _Activity = RigMonitorInstrumentation.StartActivity(_Service + " " + _Operation, kind);

            if (_Activity != null)
            {
                try
                {
                    _Activity.SetTag(RigMonitorTelemetryNames.LabelService, _Service);
                    _Activity.SetTag(RigMonitorTelemetryNames.LabelOperation, _Operation);
                    if (target != null && target.IsAbsoluteUri)
                    {
                        // Host and port only: full URLs can carry credentials or query strings.
                        _Activity.SetTag("server.address", target.Host);
                        _Activity.SetTag("server.port", target.Port);
                        _Activity.SetTag("url.scheme", target.Scheme);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Start instrumenting an outbound call.
        /// </summary>
        /// <param name="service">Bounded service name (for example dcgm, ollama, sqlite).</param>
        /// <param name="operation">Bounded operation name (for example metrics, version, create).</param>
        /// <param name="target">Optional target URI; only host, port, and scheme are recorded.</param>
        /// <param name="kind">Span kind. Default is <see cref="ActivityKind.Client"/>.</param>
        /// <returns>Call instrumentation; dispose when the call completes.</returns>
        public static IntegrationCall Start(string service, string operation, Uri? target = null, ActivityKind kind = ActivityKind.Client)
        {
            return new IntegrationCall(service, operation, target, kind);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Set a span attribute. Never pass secrets, payloads, or unbounded user input that is sensitive.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        public void SetTag(string key, object? value)
        {
            if (_Activity == null) return;
            try
            {
                _Activity.SetTag(key, value);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record the HTTP response status code on the span.
        /// </summary>
        /// <param name="statusCode">Status code.</param>
        public void SetHttpStatusCode(int statusCode)
        {
            SetTag("http.response.status_code", statusCode);
        }

        /// <summary>
        /// Mark the call successful.
        /// </summary>
        public void Succeed()
        {
            if (_Outcome != null) return;
            _Outcome = RigMonitorTelemetryNames.OutcomeOk;
            RigMonitorInstrumentation.SetOk(_Activity);
        }

        /// <summary>
        /// Mark the call failed with a bounded outcome and no exception (for example http_error or unavailable).
        /// </summary>
        /// <param name="outcome">Bounded outcome value.</param>
        /// <param name="description">Status description. Must not contain secrets or payloads.</param>
        public void Fail(string outcome, string? description)
        {
            if (_Outcome != null) return;
            _Outcome = String.IsNullOrWhiteSpace(outcome) ? RigMonitorTelemetryNames.OutcomeError : outcome;
            RigMonitorInstrumentation.SetError(_Activity, _Outcome, description);
            RigMonitorInstrumentation.RecordError(_Service, _Outcome);
        }

        /// <summary>
        /// Mark the call failed by exception. Cancellation requested by the caller is recorded as cancelled
        /// (span status unset), and a timeout as timeout.
        /// </summary>
        /// <param name="exception">Exception, may be null.</param>
        /// <param name="cancellationToken">Caller token, used to tell timeouts from cancellation.</param>
        public void Fail(Exception? exception, CancellationToken cancellationToken)
        {
            if (_Outcome != null) return;
            _Outcome = RigMonitorInstrumentation.ClassifyException(exception, cancellationToken);
            if (_Outcome == RigMonitorTelemetryNames.OutcomeCancelled)
            {
                SetTag(RigMonitorTelemetryNames.LabelOutcome, _Outcome);
                return;
            }

            RigMonitorInstrumentation.SetError(_Activity, exception);
            RigMonitorInstrumentation.RecordError(_Service, exception);
        }

        /// <summary>
        /// Record the call and end the span.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            if (_Outcome == null)
            {
                _Outcome = RigMonitorTelemetryNames.OutcomeError;
                RigMonitorInstrumentation.SetError(_Activity, RigMonitorTelemetryNames.OutcomeError, "Call ended without a recorded outcome.");
            }

            RigMonitorInstrumentation.RecordIntegration(_Service, _Operation, _Outcome, RigMonitorInstrumentation.ElapsedSeconds(_StartTimestamp));

            try
            {
                _Activity?.SetTag(RigMonitorTelemetryNames.LabelOutcome, _Outcome);
                _Activity?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        #endregion
    }
}
