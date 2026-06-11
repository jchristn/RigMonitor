namespace RigMonitor.Core.Database
{
    using System;
    using RigMonitor.Core.Database.Sqlite;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Factory for persistence database drivers.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a database driver.
        /// </summary>
        /// <param name="settings">Persistence settings.</param>
        /// <returns>Database driver.</returns>
        public static DatabaseDriverBase Create(PersistenceSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (settings.Database.Type == DatabaseTypeEnum.Sqlite)
            {
                return new SqliteDatabaseDriver(settings.Database, settings.Hostname);
            }

            throw new NotSupportedException("Unsupported database provider '" + settings.Database.Type + "'.");
        }
    }
}
