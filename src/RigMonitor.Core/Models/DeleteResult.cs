namespace RigMonitor.Core.Models
{
    /// <summary>
    /// Delete operation result.
    /// </summary>
    public class DeleteResult
    {
        /// <summary>
        /// Whether a single target was deleted.
        /// </summary>
        public bool Deleted { get; set; } = false;

        /// <summary>
        /// Deleted row count.
        /// </summary>
        public long DeletedCount { get; set; } = 0L;
    }
}
