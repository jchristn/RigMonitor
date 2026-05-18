namespace RigMonitor.Telemetry.Platform.Shared
{
    using System;

    /// <summary>
    /// Captured network counter sample.
    /// </summary>
    internal class NetworkInterfaceSample
    {
        internal DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
        internal long BytesReceived { get; set; } = 0L;
        internal long BytesSent { get; set; } = 0L;
    }
}
