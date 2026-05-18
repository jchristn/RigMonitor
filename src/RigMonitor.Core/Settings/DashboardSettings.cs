namespace RigMonitor.Core.Settings
{
    using System;

    /// <summary>
    /// Dashboard settings.
    /// </summary>
    public class DashboardSettings
    {
        /// <summary>
        /// Whether the dashboard is served.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Dashboard title.
        /// </summary>
        public string Title { get; set; } = Constants.DefaultDashboardTitle;

        /// <summary>
        /// Auto-refresh interval in milliseconds.
        /// Minimum 1000, maximum 3600000.
        /// </summary>
        public int AutoRefreshIntervalMs
        {
            get
            {
                return _AutoRefreshIntervalMs;
            }
            set
            {
                _AutoRefreshIntervalMs = Math.Clamp(value, 1000, 3600000);
            }
        }

        private int _AutoRefreshIntervalMs = 5000;
    }
}
