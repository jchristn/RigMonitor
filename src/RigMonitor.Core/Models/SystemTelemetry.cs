namespace RigMonitor.Core.Models
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// System metadata telemetry.
    /// </summary>
    public class SystemTelemetry
    {
        /// <summary>
        /// Machine hostname.
        /// </summary>
        public string Hostname { get; set; } = String.Empty;

        /// <summary>
        /// Host uptime in milliseconds.
        /// </summary>
        public long UptimeMs
        {
            get
            {
                return _UptimeMs;
            }
            set
            {
                _UptimeMs = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Operating system description.
        /// </summary>
        public string OsDescription { get; set; } = String.Empty;

        /// <summary>
        /// Operating system architecture.
        /// </summary>
        public Architecture OsArchitecture { get; set; } = Architecture.X64;

        /// <summary>
        /// Process architecture.
        /// </summary>
        public Architecture ProcessArchitecture { get; set; } = Architecture.X64;

        private long _UptimeMs = 0L;
    }
}
