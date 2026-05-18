namespace RigMonitor.Core.Settings
{
    using System.Collections.Generic;

    /// <summary>
    /// CORS settings.
    /// </summary>
    public class CorsSettings
    {
        /// <summary>
        /// Whether CORS is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allowed origins.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new List<string> { "*" };

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new List<string> { "GET", "POST", "PUT", "DELETE", "OPTIONS", "HEAD" };

        /// <summary>
        /// Allowed headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new List<string> { "Content-Type", "Authorization", "X-Api-Key" };

        /// <summary>
        /// Preflight max age in seconds.
        /// </summary>
        public int MaxAgeSeconds
        {
            get
            {
                return _MaxAgeSeconds;
            }
            set
            {
                _MaxAgeSeconds = value < 0 ? 0 : value;
            }
        }

        private int _MaxAgeSeconds = 86400;
    }
}
