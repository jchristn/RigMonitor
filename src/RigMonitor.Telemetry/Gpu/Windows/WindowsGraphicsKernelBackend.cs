namespace RigMonitor.Telemetry.Gpu.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using RigMonitor.Core.Observability;
    using RigMonitor.Telemetry.Platform.Windows.Native;

    /// <summary>
    /// Vendor-neutral Windows GPU backend. DXGI provides identity and budgets on demand, and a background sampler collects
    /// PDH engine utilization and adapter memory so snapshot reads never wait on a rate interval.
    /// </summary>
    public sealed class WindowsGraphicsKernelBackend : IWindowsGpuAdapterSource, IDisposable
    {
        private readonly IWindowsGpuNative _Native;
        private readonly int _IntervalMs;
        private readonly object _Lock = new object();
        private Timer? _Timer = null;
        private bool _CountersOpen = false;
        private bool _Started = false;
        private Dictionary<string, Dictionary<string, double>> _Engines = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, double> _Dedicated = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, double> _Shared = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private bool? _Available = null;

        /// <summary>
        /// Instantiate the backend.
        /// </summary>
        /// <param name="native">Native seam.</param>
        /// <param name="intervalMs">Sampler interval in milliseconds.</param>
        public WindowsGraphicsKernelBackend(IWindowsGpuNative native, int intervalMs)
        {
            _Native = native ?? throw new ArgumentNullException(nameof(native));
            _IntervalMs = Math.Clamp(intervalMs, 250, 10000);
        }

        /// <summary>
        /// Whether DXGI enumerates at least one hardware adapter.
        /// </summary>
        /// <returns>True when available.</returns>
        public bool IsAvailable()
        {
            lock (_Lock)
            {
                if (_Available.HasValue)
                {
                    return _Available.Value;
                }
            }

            bool available;
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceWindowsGraphics, "dxgi", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    available = _Native.EnumerateAdapters().Count > 0;
                    call.Succeed();
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    available = false;
                }
            }

            RigMonitorInstrumentation.SetGpuNativeFeature("dxgi", available);
            lock (_Lock)
            {
                _Available = available;
            }

            if (available)
            {
                EnsureSampler();
            }

            return available;
        }

        /// <summary>
        /// Current adapter readings.
        /// </summary>
        /// <returns>Adapters.</returns>
        public IReadOnlyList<WindowsGpuAdapterReading> GetAdapters()
        {
            IReadOnlyList<WindowsGpuAdapterReading> adapters;
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceWindowsGraphics, "dxgi", null, System.Diagnostics.ActivityKind.Internal))
            {
                try
                {
                    adapters = _Native.EnumerateAdapters();
                    call.Succeed();
                }
                catch (Exception exception)
                {
                    call.Fail(exception, CancellationToken.None);
                    RigMonitorInstrumentation.RecordGpuNativeError("qualcomm", "dxgi");
                    throw;
                }
            }

            lock (_Lock)
            {
                Apply(adapters, _Engines, _Dedicated, _Shared);
            }

            return adapters;
        }

        /// <summary>
        /// Collect one PDH sample synchronously (used by the background timer and tests).
        /// </summary>
        public void SampleOnce()
        {
            if (!_CountersOpen)
            {
                return;
            }

            try
            {
                WindowsPdhSample sample = _Native.Collect();
                Dictionary<string, Dictionary<string, double>> engines = WindowsPdhParser.AggregateEngines(sample.Engines);
                Dictionary<string, double> dedicated = WindowsPdhParser.AggregateAdapterMemory(sample.DedicatedUsage);
                Dictionary<string, double> shared = WindowsPdhParser.AggregateAdapterMemory(sample.SharedUsage);

                lock (_Lock)
                {
                    _Engines = engines;
                    _Dedicated = dedicated;
                    _Shared = shared;
                }
            }
            catch (Exception)
            {
                RigMonitorInstrumentation.RecordGpuNativeError("qualcomm", "pdh");
            }
        }

        /// <summary>
        /// Apply sampler values to adapter readings.
        /// </summary>
        /// <param name="adapters">Adapters (modified in place).</param>
        /// <param name="engines">Engine utilization by LUID.</param>
        /// <param name="dedicated">Dedicated usage by LUID.</param>
        /// <param name="shared">Shared usage by LUID.</param>
        public static void Apply(
            IReadOnlyList<WindowsGpuAdapterReading> adapters,
            Dictionary<string, Dictionary<string, double>> engines,
            Dictionary<string, double> dedicated,
            Dictionary<string, double> shared)
        {
            foreach (WindowsGpuAdapterReading adapter in adapters)
            {
                if (engines.TryGetValue(adapter.Luid, out Dictionary<string, double>? values))
                {
                    adapter.EngineUtilization = new Dictionary<string, double>(values, StringComparer.Ordinal);
                }

                if (dedicated.TryGetValue(adapter.Luid, out double dedicatedBytes))
                {
                    adapter.LocalUsageBytes = dedicatedBytes;
                }

                if (shared.TryGetValue(adapter.Luid, out double sharedBytes))
                {
                    adapter.NonLocalUsageBytes = sharedBytes;
                }
            }
        }

        /// <summary>
        /// Stop the sampler and release native resources.
        /// </summary>
        public void Dispose()
        {
            lock (_Lock)
            {
                _Timer?.Dispose();
                _Timer = null;
            }

            _Native.Dispose();
        }

        private void EnsureSampler()
        {
            lock (_Lock)
            {
                if (_Started)
                {
                    return;
                }

                _Started = true;
            }

            _CountersOpen = _Native.TryOpenCounters(out string? _);
            RigMonitorInstrumentation.SetGpuNativeFeature("pdh", _CountersOpen);
            if (!_CountersOpen)
            {
                return;
            }

            SampleOnce();
            lock (_Lock)
            {
                _Timer = new Timer(_ => SampleOnce(), null, _IntervalMs, _IntervalMs);
            }
        }
    }
}
