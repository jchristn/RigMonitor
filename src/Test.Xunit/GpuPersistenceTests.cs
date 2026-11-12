namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using RigMonitor.Core.Database;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;

    /// <summary>
    /// Vendor-aware telemetry history: schema migration from 0.2.1, vendor columns, filters, and per-vendor roll-ups.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class GpuPersistenceTests
    {
        private const string LegacySchema = @"
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

        /// <summary>
        /// A 0.2.1 database migrates in place: new columns exist, existing GPU rows are tagged nvidia, and history stays queryable.
        /// </summary>
        [Fact]
        public async Task LegacyDatabaseShouldMigrateAndBackfillVendor()
        {
            string directory = CreateTempDirectory();
            string file = Path.Combine(directory, "legacy.db");
            try
            {
                using (SqliteConnection connection = new SqliteConnection("Data Source=" + file + ";Pooling=False"))
                {
                    connection.Open();
                    Execute(connection, LegacySchema);
                    Execute(connection,
                        "INSERT INTO telemetry_samples (id, hostname, collectedutc, persistedutc, hostplatform, nvidiaavailable, ollamaavailable, vllmavailable, utilyzeavailable, gpudevicecount, gpuaverageutilizationpercent, gputotalpowerusagewatts, snapshotjson) " +
                        "VALUES ('tel_legacy', 'rig-a', '2026-06-11 05:10:00.000000', '2026-06-11 05:10:01.000000', 'Linux', 1, 0, 0, 0, 1, 40, 250, '{\"gpu\":{\"vendor\":\"NVIDIA\",\"devices\":[{\"uuid\":\"GPU-1\",\"metrics\":{\"xidErrors\":0}}]}}');");
                    Execute(connection,
                        "INSERT INTO telemetry_gpu_samples (id, sampleid, hostname, collectedutc, deviceindex, uuid, model, gpuutilizationpercent, memoryusedmegabytes, memorytotalmegabytes, powerusagewatts) " +
                        "VALUES ('tgd_legacy', 'tel_legacy', 'rig-a', '2026-06-11 05:10:00.000000', 0, 'GPU-1', 'NVIDIA RTX 4090', 40, 1000, 24576, 250);");
                }

                DatabaseDriverBase database = CreateDatabase(file);
                try
                {
                    await database.InitializeAsync(CancellationToken.None);
                    await database.InitializeAsync(CancellationToken.None);

                    TelemetryHistorySearchResult nvidia = await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter { GpuVendor = GpuVendorEnum.Nvidia }, CancellationToken.None);
                    TelemetrySampleRecord record = Assert.Single(nvidia.Data);
                    Assert.Equal(GpuVendorEnum.Nvidia, record.GpuVendor);
                    Assert.False(record.AmdAvailable);

                    TelemetrySampleDetail? detail = await database.TelemetryHistory.ReadDetailAsync("tel_legacy", CancellationToken.None);
                    Assert.Equal(GpuVendorEnum.Nvidia, detail!.Snapshot.Gpu!.Vendor);

                    TelemetryRollupResult rollup = await database.TelemetryHistory.RollupAsync(Rollup(new DateTime(2026, 6, 11, 5, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
                    GpuVendorRollup vendor = rollup.Buckets.Single(bucket => bucket.SampleCount > 0).GpuVendors!["nvidia"];
                    Assert.Equal(40D, vendor.AverageUtilizationPercent);
                    Assert.Equal(250D, vendor.AveragePowerUsageWatts);
                }
                finally
                {
                    database.Dispose();
                }

                using (SqliteConnection connection = new SqliteConnection("Data Source=" + file + ";Pooling=False"))
                {
                    connection.Open();
                    using (SqliteCommand command = new SqliteCommand("SELECT vendor, source FROM telemetry_gpu_samples WHERE id = 'tgd_legacy';", connection))
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        Assert.True(reader.Read());
                        Assert.Equal("nvidia", reader.GetString(0));
                        Assert.Equal("dcgmExporter", reader.GetString(1));
                    }
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// Mixed-vendor snapshots round-trip, filter by vendor and availability, and roll up per vendor with two-level aggregation.
        /// </summary>
        [Fact]
        public async Task MixedVendorHistoryShouldFilterAndRollUpPerVendor()
        {
            string directory = CreateTempDirectory();
            DatabaseDriverBase database = CreateDatabase(Path.Combine(directory, "mixed.db"));
            try
            {
                await database.InitializeAsync(CancellationToken.None);
                DateTime bucketStart = new DateTime(2026, 11, 12, 6, 0, 0, DateTimeKind.Utc);

                TelemetrySampleDetail mixed = await database.TelemetryHistory.CreateAsync(MixedSnapshot(bucketStart.AddMinutes(5), 20D, 60D), CancellationToken.None);
                await database.TelemetryHistory.CreateAsync(MixedSnapshot(bucketStart.AddMinutes(20), 40D, 80D), CancellationToken.None);
                TelemetrySampleDetail apple = await database.TelemetryHistory.CreateAsync(AppleSnapshot(bucketStart.AddMinutes(30)), CancellationToken.None);

                Assert.Equal(GpuVendorEnum.Mixed, mixed.GpuVendor);
                Assert.Equal(GpuVendorEnum.Apple, apple.GpuVendor);
                Assert.Null(apple.GpuTotalPowerUsageWatts);
                Assert.Null(apple.GpuAverageTemperatureCelsius);

                Assert.Equal(2L, (await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter { GpuVendor = GpuVendorEnum.Amd }, CancellationToken.None)).TotalCount);
                Assert.Equal(1L, (await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter { GpuVendor = GpuVendorEnum.Apple }, CancellationToken.None)).TotalCount);
                Assert.Equal(1L, (await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter { AppleGpuAvailable = true }, CancellationToken.None)).TotalCount);
                Assert.Equal(2L, (await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter { AmdAvailable = true }, CancellationToken.None)).TotalCount);
                Assert.Equal(0L, (await database.TelemetryHistory.SearchAsync(new TelemetryHistorySearchFilter { QualcommAvailable = true }, CancellationToken.None)).TotalCount);

                TelemetrySampleDetail? detail = await database.TelemetryHistory.ReadDetailAsync(mixed.Id, CancellationToken.None);
                Assert.Equal(2, detail!.Snapshot.Amd!.Devices.Count);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, detail.Snapshot.Amd.Source);
                Assert.Equal(GpuVendorEnum.Mixed, detail.Snapshot.Gpu!.Vendor);

                TelemetryRollupResult rollup = await database.TelemetryHistory.RollupAsync(Rollup(bucketStart), CancellationToken.None);
                TelemetryRollupBucket bucket = rollup.Buckets.Single(item => item.SampleCount > 0);
                Assert.Equal(3L, bucket.SampleCount);

                GpuVendorRollup nvidia = bucket.GpuVendors!["nvidia"];
                Assert.Equal(2L, nvidia.SampleCount);
                Assert.Equal(30D, nvidia.AverageUtilizationPercent);
                Assert.Equal(300D, nvidia.AveragePowerUsageWatts);

                GpuVendorRollup amd = bucket.GpuVendors["amd"];
                Assert.Equal(2D, amd.AverageDeviceCount);
                Assert.Equal(70D, amd.AverageUtilizationPercent);
                Assert.Equal(1000D, amd.AveragePowerUsageWatts);
                Assert.Equal(80D, amd.AverageHotspotTemperatureCelsius);
                Assert.Equal(2048D, amd.AverageGttUsedMegabytes);
                Assert.Equal(2D * 65536D, amd.AverageMemoryUsedMegabytes);

                GpuVendorRollup appleRollup = bucket.GpuVendors["apple"];
                Assert.Null(appleRollup.AveragePowerUsageWatts);
                Assert.Null(appleRollup.AverageTemperatureCelsius);
                Assert.Equal(110000D, appleRollup.AverageWorkingSetMegabytes);
                Assert.Equal(2D, appleRollup.MaxThermalState);

                TelemetryRollupRequest amdOnlyRequest = Rollup(bucketStart);
                amdOnlyRequest.GpuVendor = GpuVendorEnum.Amd;
                TelemetryRollupBucket amdOnly = (await database.TelemetryHistory.RollupAsync(amdOnlyRequest, CancellationToken.None)).Buckets.Single(item => item.SampleCount > 0);
                Assert.Equal(2L, amdOnly.SampleCount);
                Assert.Equal(70D, amdOnly.AverageGpuUtilizationPercent);
                Assert.Equal(1000D, amdOnly.AverageGpuPowerUsageWatts);
                Assert.Equal(2D, amdOnly.AverageGpuDeviceCount);
                Assert.Equal(new[] { "amd" }, amdOnly.GpuVendors!.Keys);

                TelemetryRollupRequest noBreakdown = Rollup(bucketStart);
                noBreakdown.IncludeGpuVendorBreakdown = false;
                Assert.All((await database.TelemetryHistory.RollupAsync(noBreakdown, CancellationToken.None)).Buckets, item => Assert.Null(item.GpuVendors));

                Assert.Equal(3L, await database.TelemetryHistory.DeleteOlderThanAsync(bucketStart.AddHours(2), CancellationToken.None));
                TelemetryRollupResult empty = await database.TelemetryHistory.RollupAsync(Rollup(bucketStart), CancellationToken.None);
                Assert.All(empty.Buckets, item => Assert.Null(item.GpuVendors));
            }
            finally
            {
                database.Dispose();
                DeleteDirectory(directory);
            }
        }

        private static TelemetryRollupRequest Rollup(DateTime start)
        {
            return new TelemetryRollupRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(1),
                BucketMinutes = 60,
                IncludeEmptyBuckets = false
            };
        }

        private static TelemetrySnapshot MixedSnapshot(DateTime collectedUtc, double nvidiaUtilization, double amdUtilization)
        {
            NvidiaGpuTelemetry nvidia = new NvidiaGpuTelemetry { Devices = { Nvidia(nvidiaUtilization) } };
            AmdGpuTelemetry amd = new AmdGpuTelemetry
            {
                Source = GpuTelemetrySourceEnum.AmdSmiLibrary,
                Devices = { Amd(0, amdUtilization), Amd(1, amdUtilization) }
            };

            return new TelemetrySnapshot
            {
                CollectedUtc = collectedUtc,
                HostPlatform = HostPlatformEnum.Linux,
                NvidiaAvailable = true,
                AmdAvailable = true,
                Nvidia = nvidia,
                Amd = amd,
                Gpu = RigMonitor.Telemetry.Gpu.GpuAggregator.Build(nvidia, amd, null, null)
            };
        }

        private static TelemetrySnapshot AppleSnapshot(DateTime collectedUtc)
        {
            AppleGpuDeviceTelemetry device = new AppleGpuDeviceTelemetry
            {
                Model = "Apple M5 Max",
                RecommendedMaxWorkingSetMegabytes = 110000D,
                ThermalState = GpuThermalStateEnum.Serious
            };
            device.Metrics.GpuUtilizationPercent = 10D;
            device.Metrics.MemoryUsedMegabytes = 1024D;
            device.Metrics.MemoryTotalMegabytes = 131072D;
            AppleGpuTelemetry apple = new AppleGpuTelemetry { Devices = { device } };

            return new TelemetrySnapshot
            {
                CollectedUtc = collectedUtc,
                HostPlatform = HostPlatformEnum.Mac,
                AppleGpuAvailable = true,
                Apple = apple,
                Gpu = RigMonitor.Telemetry.Gpu.GpuAggregator.Build(null, null, apple, null)
            };
        }

        private static NvidiaGpuDeviceTelemetry Nvidia(double utilization)
        {
            NvidiaGpuDeviceTelemetry device = new NvidiaGpuDeviceTelemetry { Uuid = "GPU-N", Model = "NVIDIA L40S", XidErrors = 0 };
            device.Metrics.GpuUtilizationPercent = utilization;
            device.Metrics.MemoryUsedMegabytes = 1000D;
            device.Metrics.MemoryTotalMegabytes = 46080D;
            device.Metrics.TemperatureCelsius = 60D;
            device.Metrics.PowerUsageWatts = 300D;
            return device;
        }

        private static AmdGpuDeviceTelemetry Amd(int index, double utilization)
        {
            AmdGpuDeviceTelemetry device = new AmdGpuDeviceTelemetry
            {
                DeviceIndex = index,
                Source = GpuTelemetrySourceEnum.AmdSmiLibrary,
                Uuid = "amd-" + index,
                Model = "AMD Instinct MI300X",
                HotspotTemperatureCelsius = 80D,
                GttUsedMegabytes = 1024D,
                PartitionId = "0"
            };
            device.Metrics.GpuUtilizationPercent = utilization;
            device.Metrics.MemoryUsedMegabytes = 65536D;
            device.Metrics.MemoryTotalMegabytes = 196608D;
            device.Metrics.TemperatureCelsius = 70D;
            device.Metrics.PowerUsageWatts = 500D;
            return device;
        }

        private static DatabaseDriverBase CreateDatabase(string file)
        {
            return DatabaseDriverFactory.Create(new PersistenceSettings
            {
                Hostname = "rig-a",
                Database = new DatabaseSettings { Filename = file }
            });
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "RigMonitorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
