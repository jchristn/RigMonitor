namespace RigMonitor.Core.Database.Sqlite
{
    using System;
    using System.Data;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using RigMonitor.Core.Database.Interfaces;
    using RigMonitor.Core.Database.Sqlite.Implementations;
    using RigMonitor.Core.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite database driver.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        /// <inheritdoc />
        public override ITelemetryHistoryMethods TelemetryHistory { get; }

        /// <summary>
        /// Timestamp format used for SQLite storage.
        /// </summary>
        internal const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff";

        private readonly DatabaseSettings _Settings;
        private readonly string _ConnectionString;
        private readonly SemaphoreSlim _DatabaseLock = new SemaphoreSlim(1, 1);
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the SQLite database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="hostname">Configured persistence hostname.</param>
        public SqliteDatabaseDriver(DatabaseSettings settings, string hostname)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _Settings = settings;
            string fullPath = Path.GetFullPath(settings.Filename);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _ConnectionString = "Data Source=" + fullPath + ";Pooling=false";
            TelemetryHistory = new TelemetryHistoryMethods(this, String.IsNullOrWhiteSpace(hostname) ? "localhost" : hostname.Trim());
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            await ExecuteNonQueryAsync("PRAGMA journal_mode=WAL;", token).ConfigureAwait(false);
            await ExecuteNonQueryAsync(SetupQueries.CreateTablesAndIndexes(), token).ConfigureAwait(false);

            foreach (string migration in SetupQueries.GetMigrationStatements())
            {
                try
                {
                    await ExecuteNonQueryAsync(migration, token).ConfigureAwait(false);
                }
                catch (SqliteException)
                {
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_Disposed) return;
            _DatabaseLock.Dispose();
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        internal async Task<DataTable> ExecuteQueryAsync(string query, CancellationToken token, params SqliteParameter[] parameters)
        {
            if (String.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));
            token.ThrowIfCancellationRequested();

            await _DatabaseLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
                {
                    using (SqliteCommand command = new SqliteCommand(query, connection))
                    {
                        AddParameters(command, parameters);
                        using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            return LoadDataTable(reader);
                        }
                    }
                }
            }
            finally
            {
                _DatabaseLock.Release();
            }
        }

        internal async Task<int> ExecuteNonQueryAsync(string query, CancellationToken token, params SqliteParameter[] parameters)
        {
            if (String.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));
            token.ThrowIfCancellationRequested();

            await _DatabaseLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
                {
                    using (SqliteCommand command = new SqliteCommand(query, connection))
                    {
                        AddParameters(command, parameters);
                        return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _DatabaseLock.Release();
            }
        }

        internal async Task ExecuteInTransactionAsync(Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> action, CancellationToken token)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            token.ThrowIfCancellationRequested();

            await _DatabaseLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
                {
                    using (SqliteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            await action(connection, transaction, token).ConfigureAwait(false);
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            finally
            {
                _DatabaseLock.Release();
            }
        }

        internal static void AddParameters(SqliteCommand command, SqliteParameter[]? parameters)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (parameters == null) return;

            foreach (SqliteParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken token)
        {
            SqliteConnection connection = new SqliteConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            using (SqliteCommand command = new SqliteCommand("PRAGMA foreign_keys=ON;", connection))
            {
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            return connection;
        }

        private static DataTable LoadDataTable(SqliteDataReader reader)
        {
            DataTable table = new DataTable();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                DataColumn column = new DataColumn(reader.GetName(i), reader.GetFieldType(i));
                column.AllowDBNull = true;
                table.Columns.Add(column);
            }

            while (reader.Read())
            {
                DataRow row = table.NewRow();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                }
                table.Rows.Add(row);
            }

            return table;
        }
    }
}
