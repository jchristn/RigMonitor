namespace RigMonitor.Telemetry.Platform.Shared
{
    using System;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Telemetry.Platform.Linux;
    using RigMonitor.Telemetry.Platform.Mac;
    using RigMonitor.Telemetry.Platform.Windows;

    /// <summary>
    /// Creates a platform-specific system telemetry provider.
    /// </summary>
    public static class SystemTelemetryProviderFactory
    {
        /// <summary>
        /// Create a platform-specific provider.
        /// </summary>
        /// <returns>Telemetry provider.</returns>
        public static ISystemTelemetryProvider Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsSystemTelemetryProvider();
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxSystemTelemetryProvider();
            }

            return new MacSystemTelemetryProvider();
        }
    }
}
