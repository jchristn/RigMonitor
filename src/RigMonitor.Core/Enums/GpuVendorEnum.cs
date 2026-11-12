namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// GPU vendor.
    /// </summary>
    public enum GpuVendorEnum
    {
        /// <summary>
        /// Unknown vendor.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// NVIDIA.
        /// </summary>
        Nvidia = 1,
        /// <summary>
        /// AMD.
        /// </summary>
        Amd = 2,
        /// <summary>
        /// Apple.
        /// </summary>
        Apple = 3,
        /// <summary>
        /// Qualcomm.
        /// </summary>
        Qualcomm = 4,
        /// <summary>
        /// Intel (reserved).
        /// </summary>
        Intel = 5,
        /// <summary>
        /// More than one vendor is present (aggregate only).
        /// </summary>
        Mixed = 6
    }
}
