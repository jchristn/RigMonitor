namespace RigMonitor.Core.Models
{
    using System;
    using RigMonitor.Core.Enums;

    /// <summary>
    /// Query for paged enumeration APIs.
    /// </summary>
    public class EnumerationQuery
    {
        /// <summary>
        /// Maximum number of results to return. Minimum 1, maximum 1000. Default is 100.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Continuation token from a prior enumeration response.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Result ordering.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Optional hostname filter.
        /// </summary>
        public string? HostnameFilter { get; set; } = null;

        /// <summary>
        /// Optional inclusive start time filter.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// Optional inclusive end time filter.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        private int _MaxResults = 100;
    }
}
