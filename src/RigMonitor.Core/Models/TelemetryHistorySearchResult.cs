namespace RigMonitor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Paged telemetry history search result.
    /// </summary>
    public class TelemetryHistorySearchResult
    {
        /// <summary>
        /// Result records.
        /// </summary>
        public List<TelemetrySampleRecord> Data
        {
            get
            {
                return _Data;
            }
            set
            {
                _Data = value ?? new List<TelemetrySampleRecord>();
            }
        }

        /// <summary>
        /// 1-based page number.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 25;

        /// <summary>
        /// Total matching record count.
        /// </summary>
        public long TotalCount { get; set; } = 0L;

        /// <summary>
        /// Total page count.
        /// </summary>
        public int TotalPages
        {
            get
            {
                if (PageSize < 1) return 0;
                return (int)((TotalCount + PageSize - 1) / PageSize);
            }
        }

        private List<TelemetrySampleRecord> _Data = new List<TelemetrySampleRecord>();
    }
}
