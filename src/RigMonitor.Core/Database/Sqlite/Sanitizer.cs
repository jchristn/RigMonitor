namespace RigMonitor.Core.Database.Sqlite
{
    /// <summary>
    /// SQLite string sanitizer.
    /// </summary>
    internal static class Sanitizer
    {
        /// <summary>
        /// Sanitize a string for ad hoc SQL fragments.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns>Sanitized value.</returns>
        internal static string Sanitize(string? value)
        {
            return value == null ? string.Empty : value.Replace("'", "''");
        }
    }
}
