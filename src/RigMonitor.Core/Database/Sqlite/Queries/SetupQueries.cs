namespace RigMonitor.Core.Database.Sqlite.Queries
{
    /// <summary>
    /// SQLite setup queries.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Create telemetry history tables and indexes.
        /// </summary>
        /// <returns>SQL statement batch.</returns>
        internal static string CreateTablesAndIndexes()
        {
            return @"
                CREATE TABLE IF NOT EXISTS telemetry_samples (
                    id TEXT PRIMARY KEY,
                    hostname TEXT NOT NULL,
                    collectedutc TEXT NOT NULL,
                    persistedutc TEXT NOT NULL,
                    hostplatform TEXT NOT NULL,
                    nvidiaavailable INTEGER NOT NULL,
                    ollamaavailable INTEGER NOT NULL,
                    vllmavailable INTEGER NOT NULL,
                    utilyzeavailable INTEGER NOT NULL,
                    cpuutilizationpercent REAL,
                    logicalcorecount INTEGER,
                    memorytotalbytes INTEGER,
                    memoryusedbytes INTEGER,
                    memoryavailablebytes INTEGER,
                    memoryutilizationpercent REAL,
                    networkreceivebytespersecond REAL,
                    networktransmitbytespersecond REAL,
                    diskreadoperationspersecond REAL,
                    diskwriteoperationspersecond REAL,
                    diskreadqueuedepth REAL,
                    diskwritequeuedepth REAL,
                    gpudevicecount INTEGER,
                    gpuaverageutilizationpercent REAL,
                    gpuaveragememoryutilizationpercent REAL,
                    gpuaveragetemperaturecelsius REAL,
                    gputotalpowerusagewatts REAL,
                    ollamaavailablemodelcount INTEGER,
                    ollamaloadedmodelcount INTEGER,
                    vllmrunningrequests REAL,
                    vllmwaitingrequests REAL,
                    vllmgpucacheusagepercent REAL,
                    utilyzedevicecount INTEGER,
                    snapshotjson TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS telemetry_gpu_samples (
                    id TEXT PRIMARY KEY,
                    sampleid TEXT NOT NULL,
                    hostname TEXT NOT NULL,
                    collectedutc TEXT NOT NULL,
                    deviceindex INTEGER NOT NULL,
                    uuid TEXT,
                    busid TEXT,
                    model TEXT,
                    driverversion TEXT,
                    migprofile TEXT,
                    gpuutilizationpercent REAL,
                    memoryusedmegabytes REAL,
                    memoryfreemegabytes REAL,
                    memorytotalmegabytes REAL,
                    memoryutilizationpercent REAL,
                    temperaturecelsius REAL,
                    powerusagewatts REAL,
                    smclockmhz REAL,
                    memoryclockmhz REAL,
                    xiderrors INTEGER,
                    FOREIGN KEY (sampleid) REFERENCES telemetry_samples(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_telemetry_samples_collectedutc ON telemetry_samples(collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_samples_hostname_collectedutc ON telemetry_samples(hostname, collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_samples_cpu_collectedutc ON telemetry_samples(cpuutilizationpercent, collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_samples_memory_collectedutc ON telemetry_samples(memoryutilizationpercent, collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_samples_gpu_collectedutc ON telemetry_samples(gpuaverageutilizationpercent, collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_gpu_samples_sampleid ON telemetry_gpu_samples(sampleid);
                CREATE INDEX IF NOT EXISTS idx_telemetry_gpu_samples_hostname_collectedutc ON telemetry_gpu_samples(hostname, collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_gpu_samples_uuid_collectedutc ON telemetry_gpu_samples(uuid, collectedutc);
                CREATE INDEX IF NOT EXISTS idx_telemetry_gpu_samples_model_collectedutc ON telemetry_gpu_samples(model, collectedutc);
            ";
        }

        /// <summary>
        /// Migration statements for existing databases.
        /// </summary>
        /// <returns>Migration statements.</returns>
        internal static string[] GetMigrationStatements()
        {
            return new string[]
            {
                "ALTER TABLE telemetry_samples ADD COLUMN hostname TEXT NOT NULL DEFAULT 'localhost';",
                "ALTER TABLE telemetry_gpu_samples ADD COLUMN hostname TEXT NOT NULL DEFAULT 'localhost';"
            };
        }
    }
}
