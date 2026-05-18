namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// HTTP server settings.
    /// </summary>
    public class WebserverSettings
    {
        /// <summary>
        /// Bind hostname.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Bind port.
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

        /// <summary>
        /// Whether SSL is enabled.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// CORS settings.
        /// </summary>
        public CorsSettings Cors { get; set; } = new CorsSettings();

        private int _Port = 9990;
    }
}
