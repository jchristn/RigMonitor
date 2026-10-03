namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using RigMonitor.Core.Observability;

    /// <summary>
    /// In-memory listener for the RigMonitor and Watson meters and activity sources, built only on the BCL
    /// (MeterListener and ActivityListener). Use one per test and dispose it when done.
    /// Captures from every thread, so assertions should look for the specific measurements a test produces.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class TelemetryCapture : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Captured measurements.
        /// </summary>
        public List<RecordedMeasurement> Measurements
        {
            get
            {
                return _Measurements.ToList();
            }
        }

        /// <summary>
        /// Captured, stopped activities.
        /// </summary>
        public List<Activity> Activities
        {
            get
            {
                return _Activities.ToList();
            }
        }

        #endregion

        #region Private-Members

        private readonly ConcurrentQueue<RecordedMeasurement> _Measurements = new ConcurrentQueue<RecordedMeasurement>();
        private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private readonly HashSet<string> _SourceNames;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start capturing the RigMonitor and Watson sources.
        /// </summary>
        public TelemetryCapture()
        {
            _SourceNames = new HashSet<string>(StringComparer.Ordinal)
            {
                RigMonitorTelemetryNames.MeterName,
                RigMonitorTelemetryNames.WatsonSourceName
            };

            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (_SourceNames.Contains(instrument.Meter.Name)) listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Record(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Record(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Record(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener();
            _ActivityListener.ShouldListenTo = source => _SourceNames.Contains(source.Name);
            _ActivityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
            _ActivityListener.ActivityStopped = activity => _Activities.Enqueue(activity);
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read observable gauges now so their current values are captured.
        /// </summary>
        public void CollectObservables()
        {
            _MeterListener.RecordObservableInstruments();
        }

        /// <summary>
        /// Find measurements by instrument name and optional tags.
        /// </summary>
        /// <param name="instrumentName">Instrument name.</param>
        /// <param name="tags">Expected tags as alternating key, value strings.</param>
        /// <returns>Matching measurements.</returns>
        public List<RecordedMeasurement> Find(string instrumentName, params string[] tags)
        {
            return _Measurements.Where(m => m.InstrumentName == instrumentName && m.HasTags(tags)).ToList();
        }

        /// <summary>
        /// Sum of matching measurement values.
        /// </summary>
        /// <param name="instrumentName">Instrument name.</param>
        /// <param name="tags">Expected tags as alternating key, value strings.</param>
        /// <returns>Sum.</returns>
        public double Sum(string instrumentName, params string[] tags)
        {
            return Find(instrumentName, tags).Sum(m => m.Value);
        }

        /// <summary>
        /// Find stopped spans by display name.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <returns>Matching spans.</returns>
        public List<Activity> Spans(string name)
        {
            return _Activities.Where(a => a.DisplayName == name || a.OperationName == name).ToList();
        }

        /// <summary>
        /// Stop capturing.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose listeners.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;
            if (disposing)
            {
                _MeterListener.Dispose();
                _ActivityListener.Dispose();
            }
        }

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            RecordedMeasurement measurement = new RecordedMeasurement
            {
                MeterName = instrument.Meter.Name,
                InstrumentName = instrument.Name,
                Unit = instrument.Unit,
                Value = value
            };

            foreach (KeyValuePair<string, object?> tag in tags)
            {
                measurement.Tags[tag.Key] = tag.Value?.ToString();
            }

            _Measurements.Enqueue(measurement);
        }

        #endregion
    }
}
