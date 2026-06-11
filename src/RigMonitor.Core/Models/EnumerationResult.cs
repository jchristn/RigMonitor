namespace RigMonitor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Paged enumeration result.
    /// </summary>
    /// <typeparam name="T">Object type.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>
        /// Whether the enumeration succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Maximum result count requested.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = Math.Max(1, value);
            }
        }

        /// <summary>
        /// Total matching records.
        /// </summary>
        public long TotalRecords
        {
            get
            {
                return _TotalRecords;
            }
            set
            {
                _TotalRecords = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Matching records remaining after this page.
        /// </summary>
        public long RecordsRemaining
        {
            get
            {
                return _RecordsRemaining;
            }
            set
            {
                _RecordsRemaining = Math.Max(0L, value);
            }
        }

        /// <summary>
        /// Continuation token for the next page.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Whether the end of results has been reached.
        /// </summary>
        public bool EndOfResults { get; set; } = true;

        /// <summary>
        /// Total elapsed milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0D;

        /// <summary>
        /// Objects in this page.
        /// </summary>
        [JsonPropertyOrder(999)]
        public List<T> Objects
        {
            get
            {
                return _Objects;
            }
            set
            {
                _Objects = value ?? new List<T>();
            }
        }

        private int _MaxResults = 100;
        private long _TotalRecords = 0L;
        private long _RecordsRemaining = 0L;
        private List<T> _Objects = new List<T>();
    }
}
