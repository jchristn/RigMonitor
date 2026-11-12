namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// Operating-system thermal state.
    /// </summary>
    public enum GpuThermalStateEnum
    {
        /// <summary>
        /// No thermal pressure.
        /// </summary>
        Nominal = 0,
        /// <summary>
        /// Slightly elevated thermal pressure.
        /// </summary>
        Fair = 1,
        /// <summary>
        /// High thermal pressure; performance is being reduced.
        /// </summary>
        Serious = 2,
        /// <summary>
        /// Critical thermal pressure.
        /// </summary>
        Critical = 3
    }
}
