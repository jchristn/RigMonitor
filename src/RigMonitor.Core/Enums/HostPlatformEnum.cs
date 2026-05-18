namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// Supported runtime host platforms.
    /// </summary>
    public enum HostPlatformEnum
    {
        /// <summary>
        /// Platform could not be determined.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// Microsoft Windows.
        /// </summary>
        Windows = 1,
        /// <summary>
        /// Linux.
        /// </summary>
        Linux = 2,
        /// <summary>
        /// Apple macOS.
        /// </summary>
        Mac = 3
    }
}
