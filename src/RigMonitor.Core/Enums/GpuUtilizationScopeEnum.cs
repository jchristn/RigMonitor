namespace RigMonitor.Core.Enums
{
    /// <summary>
    /// What a reported GPU utilization value covers.
    /// </summary>
    public enum GpuUtilizationScopeEnum
    {
        /// <summary>
        /// Whole-device utilization reported by the driver.
        /// </summary>
        Device = 0,
        /// <summary>
        /// Sum of per-process usage across every process on the host.
        /// </summary>
        AllProcesses = 1,
        /// <summary>
        /// Sum of per-process usage, limited to processes RigMonitor is permitted to inspect.
        /// </summary>
        OwnProcesses = 2
    }
}
