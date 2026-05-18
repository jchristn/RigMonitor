namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// Syslog destination settings.
    /// </summary>
    public class SyslogServerSettings
    {
        /// <summary>
        /// Syslog hostname.
        /// </summary>
        public string Hostname { get; set; } = String.Empty;

        /// <summary>
        /// Syslog port.
        /// Minimum 1, maximum 65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = Math.Clamp(value, 1, 65535);
            }
        }

        private int _Port = 514;
    }
}
