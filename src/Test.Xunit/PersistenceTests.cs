namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core;
    using RigMonitor.Core.Database;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Helpers;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Serialization;
    using RigMonitor.Server.Services;

    /// <summary>
    /// Telemetry persistence tests.
    /// </summary>
    public class PersistenceTests
    {
        /// <summary>
        /// Verify persistence settings defaults and clamp behavior.
        /// </summary>
        [Fact]
        public void ShouldClampPersistenceSettingsAndDefaultHostname()
        {
            PersistenceSettings defaults = new PersistenceSettings();
            Assert.Equal(15000, defaults.CollectionIntervalMs);

            PersistenceSettings settings = new PersistenceSettings
            {
                Hostname = "   ",
                CollectionIntervalMs = 10,
                RetentionDays = 0,
                PruneIntervalMinutes = 0
            };

            Assert.True(settings.Enabled);
            Assert.Equal("localhost", settings.Hostname);
            Assert.Equal(1000, settings.CollectionIntervalMs);
            Assert.Equal(1, settings.RetentionDays);
            Assert.Equal(1, settings.PruneIntervalMinutes);

            settings.CollectionIntervalMs = 100000000;
            settings.RetentionDays = 10000;
            settings.PruneIntervalMinutes = 2000;

            Assert.Equal(86400000, settings.CollectionIntervalMs);
            Assert.Equal(3650, settings.RetentionDays);
            Assert.Equal(1440, settings.PruneIntervalMinutes);
        }

        /// <summary>
        /// Verify PrettyId identifiers use the expected prefixes and maximum length.
        /// </summary>
        [Fact]
        public void ShouldGeneratePrettyIdsWithPrefixesAndMaxLength()
        {
            string sampleId = IdGenerator.NewTelemetrySampleId();
            string gpuId = IdGenerator.NewTelemetryGpuSampleId();

            Assert.StartsWith(Constants.TelemetrySampleIdentifierPrefix, sampleId);
            Assert.StartsWith(Constants.TelemetryGpuSampleIdentifierPrefix, gpuId);
            Assert.True(sampleId.Length <= Constants.IdentifierLength);
            Assert.True(gpuId.Length <= Constants.IdentifierLength);
            Assert.NotEqual(sampleId, IdGenerator.NewTelemetrySampleId());
        }

        /// <summary>
        /// Verify unsupported database providers are rejected clearly.
        /// </summary>
        [Fact]
        public void ShouldRejectUnsupportedDatabaseProviders()
        {
            PersistenceSettings settings = new PersistenceSettings
            {
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Postgresql
                }
            };

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => DatabaseDriverFactory.Create(settings));
            Assert.Contains("Unsupported database provider", exception.Message);
        }

        /// <summary>
        /// Verify first-run settings creation writes persistence defaults.
        /// </summary>
        [Fact]
        public async Task ShouldCreateDefaultSettingsFileWithPersistenceDefaults()
        {
            string tempDirectory = CreateTempDirectory();
            string settingsFile = Path.Combine(tempDirectory, "rigmonitor.json");

            try
            {
                RigMonitorSettings settings = await SettingsManager.LoadAsync(settingsFile, CancellationToken.None);
                string json = await File.ReadAllTextAsync(settingsFile, CancellationToken.None);

                Assert.True(File.Exists(settingsFile));
                Assert.Equal(LogSeverityEnum.Debug, settings.Logging.MinimumSeverity);
                Assert.True(settings.Persistence.Enabled);
                Assert.Equal("localhost", settings.Persistence.Hostname);
                Assert.Equal(15000, settings.Persistence.CollectionIntervalMs);
                Assert.Equal(30, settings.Persistence.RetentionDays);
                Assert.Equal(Constants.DefaultTelemetryHistoryDatabaseFilename, settings.Persistence.Database.Filename);
                Assert.Contains("\"persistence\"", json);
                Assert.Contains("\"minimumSeverity\": \"debug\"", json);
                Assert.Contains("\"collectionIntervalMs\": 15000", json);
                Assert.Contains("\"retentionDays\": 30", json);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// Verify existing settings files are rewritten with newly introduced default properties.
        /// </summary>
        [Fact]
        public async Task ShouldRewriteExistingSettingsFileWithMissingDefaults()
        {
            string tempDirectory = CreateTempDirectory();
            string settingsFile = Path.Combine(tempDirectory, "rigmonitor.json");
            string oldSettingsJson = @"{
  ""webserver"": {
    ""hostname"": ""0.0.0.0"",
    ""port"": 9995,
    ""ssl"": false
  },
  ""telemetry"": {
    ""ollamaBaseUrl"": ""http://legacy-host:11434""
  },
  ""logging"": {
    ""logDirectory"": ""custom/logs"",
    ""minimumSeverity"": ""info""
  }
}";

            try
            {
                await File.WriteAllTextAsync(settingsFile, oldSettingsJson, CancellationToken.None);

                RigMonitorSettings settings = await SettingsManager.LoadAsync(settingsFile, CancellationToken.None);
                string rewrittenJson = await File.ReadAllTextAsync(settingsFile, CancellationToken.None);

                Assert.Equal("0.0.0.0", settings.Webserver.Hostname);
                Assert.Equal(9995, settings.Webserver.Port);
                Assert.Equal("http://legacy-host:11434", settings.Telemetry.OllamaBaseUrl);
                Assert.Equal("custom/logs", settings.Logging.LogDirectory);
                Assert.Equal(LogSeverityEnum.Info, settings.Logging.MinimumSeverity);
                Assert.True(settings.Persistence.Enabled);
                Assert.Equal("localhost", settings.Persistence.Hostname);
                Assert.Equal(15000, settings.Persistence.CollectionIntervalMs);
                Assert.Equal(30, settings.Persistence.RetentionDays);
                Assert.Contains("\"persistence\"", rewrittenJson);
                Assert.Contains("\"collectionIntervalMs\": 15000", rewrittenJson);
                Assert.Contains("\"retentionDays\": 30", rewrittenJson);
                Assert.Contains("\"minimumSeverity\": \"info\"", rewrittenJson);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// Verify SQLite initialization, persistence, search, roll-up, and retention pruning.
        /// </summary>
        [Fact]
        public async Task ShouldPersistSearchRollupAndPruneTelemetryHistory()
        {
            string tempDirectory = CreateTempDirectory();
            string databaseFile = Path.Combine(tempDirectory, "history.db");

            PersistenceSettings settings = new PersistenceSettings
            {
                Hostname = "rig-a",
                Database = new DatabaseSettings
                {
                    Filename = databaseFile
                }
            };

            DatabaseDriverBase database = DatabaseDriverFactory.Create(settings);

            try
            {
                await database.InitializeAsync(CancellationToken.None);
                await database.InitializeAsync(CancellationToken.None);

                DateTime bucketStartUtc = new DateTime(2026, 6, 11, 5, 0, 0, DateTimeKind.Utc);
                TelemetrySampleDetail first = await database.TelemetryHistory.CreateAsync(
                    BuildSnapshot(bucketStartUtc.AddMinutes(10), 20D, 50D, 30D, 60D, "GPU-1", "NVIDIA RTX 6000"),
                    CancellationToken.None);
                TelemetrySampleDetail second = await database.TelemetryHistory.CreateAsync(
                    BuildSnapshot(bucketStartUtc.AddMinutes(40), 40D, 70D, 50D, 66D, "GPU-2", "NVIDIA RTX 4090"),
                    CancellationToken.None);

                Assert.StartsWith(Constants.TelemetrySampleIdentifierPrefix, first.Id);
                Assert.True(first.Id.Length <= Constants.IdentifierLength);
                Assert.Equal("rig-a", first.Hostname);

                TelemetrySampleDetail? detail = await database.TelemetryHistory.ReadDetailAsync(first.Id, CancellationToken.None);
                Assert.NotNull(detail);
                Assert.Equal(20D, detail.Snapshot.Cpu?.UtilizationPercent);
                Assert.Equal("GPU-1", detail.Snapshot.Gpu?.Devices.First().Uuid);

                EnumerationResult<TelemetrySampleRecord> firstPage = await database.TelemetryHistory.EnumerateAsync(
                    new EnumerationQuery
                    {
                        MaxResults = 1,
                        Ordering = EnumerationOrderEnum.CreatedDescending,
                        StartUtc = bucketStartUtc,
                        EndUtc = bucketStartUtc.AddHours(1)
                    },
                    CancellationToken.None);

                Assert.True(firstPage.Success);
                Assert.Equal(2L, firstPage.TotalRecords);
                Assert.Equal(1L, firstPage.RecordsRemaining);
                Assert.False(firstPage.EndOfResults);
                Assert.NotNull(firstPage.ContinuationToken);
                Assert.Single(firstPage.Objects);
                Assert.Equal(second.Id, firstPage.Objects[0].Id);

                EnumerationResult<TelemetrySampleRecord> secondPage = await database.TelemetryHistory.EnumerateAsync(
                    new EnumerationQuery
                    {
                        MaxResults = 1,
                        ContinuationToken = firstPage.ContinuationToken,
                        Ordering = EnumerationOrderEnum.CreatedDescending,
                        StartUtc = bucketStartUtc,
                        EndUtc = bucketStartUtc.AddHours(1)
                    },
                    CancellationToken.None);

                Assert.Equal(0L, secondPage.RecordsRemaining);
                Assert.True(secondPage.EndOfResults);
                Assert.Single(secondPage.Objects);
                Assert.Equal(first.Id, secondPage.Objects[0].Id);

                TelemetryHistorySearchResult search = await database.TelemetryHistory.SearchAsync(
                    new TelemetryHistorySearchFilter
                    {
                        Hostname = "rig-a",
                        StartUtc = bucketStartUtc,
                        EndUtc = bucketStartUtc.AddHours(1),
                        PageSize = 10
                    },
                    CancellationToken.None);

                Assert.Equal(2L, search.TotalCount);
                Assert.Equal(second.Id, search.Data[0].Id);

                TelemetryHistorySearchResult gpuSearch = await database.TelemetryHistory.SearchAsync(
                    new TelemetryHistorySearchFilter
                    {
                        GpuUuid = "GPU-1"
                    },
                    CancellationToken.None);

                Assert.Equal(1L, gpuSearch.TotalCount);
                Assert.Equal(first.Id, gpuSearch.Data[0].Id);

                TelemetryHistorySearchResult thresholdSearch = await database.TelemetryHistory.SearchAsync(
                    new TelemetryHistorySearchFilter
                    {
                        NvidiaAvailable = true,
                        MinCpuUtilizationPercent = 35D,
                        MinGpuTemperatureCelsius = 65D
                    },
                    CancellationToken.None);

                Assert.Equal(1L, thresholdSearch.TotalCount);
                Assert.Equal(second.Id, thresholdSearch.Data[0].Id);

                TelemetryRollupResult rollup = await database.TelemetryHistory.RollupAsync(
                    new TelemetryRollupRequest
                    {
                        Hostname = "rig-a",
                        StartUtc = bucketStartUtc,
                        EndUtc = bucketStartUtc.AddHours(1),
                        BucketMinutes = 60,
                        IncludeEmptyBuckets = true
                    },
                    CancellationToken.None);

                Assert.Single(rollup.Buckets);
                Assert.Equal(2L, rollup.TotalSamples);
                Assert.Equal(2L, rollup.Buckets[0].SampleCount);
                Assert.Equal(30D, rollup.Buckets[0].AverageCpuUtilizationPercent);
                Assert.Equal(16D, rollup.Buckets[0].AverageLogicalCoreCount);
                Assert.Equal(60D, rollup.Buckets[0].AverageMemoryUtilizationPercent);
                Assert.Equal(1024D * 1024D * 1024D, rollup.Buckets[0].AverageMemoryTotalBytes);
                Assert.Equal(644245094D, rollup.Buckets[0].AverageMemoryUsedBytes);
                Assert.Equal(429496729.5D, rollup.Buckets[0].AverageMemoryAvailableBytes);
                Assert.Equal(1000D, rollup.Buckets[0].AverageNetworkReceiveBytesPerSecond);
                Assert.Equal(500D, rollup.Buckets[0].AverageNetworkTransmitBytesPerSecond);
                Assert.Equal(10D, rollup.Buckets[0].AverageDiskReadOperationsPerSecond);
                Assert.Equal(20D, rollup.Buckets[0].AverageDiskWriteOperationsPerSecond);
                Assert.Equal(1D, rollup.Buckets[0].AverageDiskReadQueueDepth);
                Assert.Equal(2D, rollup.Buckets[0].AverageDiskWriteQueueDepth);
                Assert.Equal(1D, rollup.Buckets[0].AverageGpuDeviceCount);
                Assert.Equal(40D, rollup.Buckets[0].AverageGpuUtilizationPercent);
                Assert.Equal(30D, rollup.Buckets[0].MinGpuUtilizationPercent);
                Assert.Equal(50D, rollup.Buckets[0].MaxGpuUtilizationPercent);
                Assert.Equal(50D, rollup.Buckets[0].AverageGpuMemoryUtilizationPercent);
                Assert.Equal(12288D, rollup.Buckets[0].AverageGpuMemoryUsedMegabytes);
                Assert.Equal(24576D, rollup.Buckets[0].AverageGpuMemoryTotalMegabytes);
                Assert.Equal(63D, rollup.Buckets[0].AverageGpuTemperatureCelsius);
                Assert.Equal(60D, rollup.Buckets[0].MinGpuTemperatureCelsius);
                Assert.Equal(66D, rollup.Buckets[0].MaxGpuTemperatureCelsius);
                Assert.Equal(250D, rollup.Buckets[0].AverageGpuPowerUsageWatts);
                Assert.Equal(4D, rollup.Buckets[0].AverageOllamaAvailableModelCount);
                Assert.Equal(2D, rollup.Buckets[0].AverageOllamaLoadedModelCount);
                Assert.Equal(3D, rollup.Buckets[0].AverageVllmRunningRequests);
                Assert.Equal(1D, rollup.Buckets[0].AverageVllmWaitingRequests);
                Assert.Equal(44D, rollup.Buckets[0].AverageVllmGpuCacheUsagePercent);
                Assert.Equal(1D, rollup.Buckets[0].AverageUtilyzeDeviceCount);

                TelemetryRollupResult trendRollup = await database.TelemetryHistory.RollupAsync(
                    new TelemetryRollupRequest
                    {
                        Hostname = "rig-a",
                        StartUtc = bucketStartUtc,
                        EndUtc = bucketStartUtc.AddHours(1),
                        BucketMinutes = 15,
                        IncludeEmptyBuckets = true
                    },
                    CancellationToken.None);

                Assert.Equal(4, trendRollup.Buckets.Count);
                Assert.Equal(1L, trendRollup.Buckets[0].SampleCount);
                Assert.Equal(0L, trendRollup.Buckets[1].SampleCount);
                Assert.Equal(1L, trendRollup.Buckets[2].SampleCount);
                Assert.Equal(0L, trendRollup.Buckets[3].SampleCount);

                TelemetryRollupResult fractionalRangeRollup = await database.TelemetryHistory.RollupAsync(
                    new TelemetryRollupRequest
                    {
                        Hostname = "rig-a",
                        StartUtc = bucketStartUtc.AddMilliseconds(123),
                        EndUtc = bucketStartUtc.AddHours(1).AddMilliseconds(456),
                        BucketMinutes = 15,
                        IncludeEmptyBuckets = true
                    },
                    CancellationToken.None);

                Assert.Equal(2L, fractionalRangeRollup.TotalSamples);
                Assert.Equal(2, fractionalRangeRollup.Buckets.Count(bucket => bucket.SampleCount > 0L));
                Assert.Equal(20D, fractionalRangeRollup.Buckets[0].AverageCpuUtilizationPercent);
                Assert.Equal(40D, fractionalRangeRollup.Buckets[2].AverageCpuUtilizationPercent);

                TelemetryRollupRequest clampedRequest = new TelemetryRollupRequest
                {
                    BucketMinutes = 0
                };

                Assert.Equal(1, clampedRequest.BucketMinutes);
                await Assert.ThrowsAsync<ArgumentException>(() => database.TelemetryHistory.RollupAsync(
                    new TelemetryRollupRequest
                    {
                        StartUtc = bucketStartUtc,
                        EndUtc = bucketStartUtc
                    },
                    CancellationToken.None));

                long deleted = await database.TelemetryHistory.DeleteOlderThanAsync(bucketStartUtc.AddMinutes(30), CancellationToken.None);
                Assert.Equal(1L, deleted);
                Assert.Null(await database.TelemetryHistory.ReadDetailAsync(first.Id, CancellationToken.None));
                Assert.NotNull(await database.TelemetryHistory.ReadDetailAsync(second.Id, CancellationToken.None));
            }
            finally
            {
                database.Dispose();
                DeleteDirectory(tempDirectory);
            }
        }

        private static TelemetrySnapshot BuildSnapshot(
            DateTime collectedUtc,
            double cpuPercent,
            double memoryPercent,
            double gpuPercent,
            double gpuTemperature,
            string gpuUuid,
            string gpuModel)
        {
            return new TelemetrySnapshot
            {
                CollectedUtc = collectedUtc,
                HostPlatform = HostPlatformEnum.Windows,
                NvidiaAvailable = true,
                OllamaAvailable = true,
                VllmAvailable = true,
                UtilyzeAvailable = true,
                System = new SystemTelemetry
                {
                    Hostname = "system-host"
                },
                Cpu = new CpuTelemetry
                {
                    LogicalCoreCount = 16,
                    UtilizationPercent = cpuPercent
                },
                Memory = new MemoryTelemetry
                {
                    TotalBytes = 1024L * 1024L * 1024L,
                    UsedBytes = (long)(1024L * 1024L * 1024L * (memoryPercent / 100D)),
                    AvailableBytes = (long)(1024L * 1024L * 1024L * (1D - memoryPercent / 100D)),
                    UtilizationPercent = memoryPercent
                },
                Network = new NetworkTelemetry
                {
                    TotalReceiveBytesPerSecond = 1000D,
                    TotalTransmitBytesPerSecond = 500D,
                    ActiveInterfaceCount = 1
                },
                Disk = new DiskTelemetry
                {
                    ReadOperationsPerSecond = 10D,
                    WriteOperationsPerSecond = 20D,
                    ReadQueueDepth = 1D,
                    WriteQueueDepth = 2D
                },
                Gpu = new GpuTelemetry
                {
                    Vendor = "NVIDIA",
                    ExporterEndpoint = "http://localhost:9400/metrics",
                    Devices =
                    {
                        new GpuDeviceTelemetry
                        {
                            DeviceIndex = 0,
                            Uuid = gpuUuid,
                            Model = gpuModel,
                            Metrics = new GpuUtilizationTelemetry
                            {
                                GpuUtilizationPercent = gpuPercent,
                                MemoryUsedMegabytes = 12288D,
                                MemoryFreeMegabytes = 12288D,
                                MemoryTotalMegabytes = 24576D,
                                TemperatureCelsius = gpuTemperature,
                                PowerUsageWatts = 250D
                            }
                        }
                    }
                },
                Ollama = new OllamaTelemetry
                {
                    Available = true,
                    AvailableModelCount = 4,
                    LoadedModelCount = 2
                },
                Vllm = new VllmTelemetry
                {
                    Available = true,
                    Summary = new VllmSummaryTelemetry
                    {
                        RunningRequests = 3D,
                        WaitingRequests = 1D,
                        GpuCacheUsagePercent = 44D
                    }
                },
                Utilyze = new UtilyzeTelemetry
                {
                    Available = true,
                    DeviceIds = { 0 },
                    Devices =
                    {
                        new UtilyzeDeviceTelemetry
                        {
                            DeviceIndex = 0,
                            Online = true
                        }
                    }
                }
            };
        }

        private static string CreateTempDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "RigMonitorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) return;
            if (!Directory.Exists(directory)) return;
            Directory.Delete(directory, true);
        }
    }
}
