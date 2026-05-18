namespace RigMonitor.Telemetry.Platform.Shared
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Runtime platform helpers.
    /// </summary>
    internal static class PlatformHelpers
    {
        internal static HostPlatformEnum GetHostPlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                return HostPlatformEnum.Windows;
            }

            if (OperatingSystem.IsLinux())
            {
                return HostPlatformEnum.Linux;
            }

            if (OperatingSystem.IsMacOS())
            {
                return HostPlatformEnum.Mac;
            }

            return HostPlatformEnum.Unknown;
        }
    }
}
