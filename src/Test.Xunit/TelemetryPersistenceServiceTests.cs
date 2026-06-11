namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Database;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Services;
    using Test.Shared;

    /// <summary>
    /// Telemetry persistence worker tests.
    /// </summary>
    public class TelemetryPersistenceServiceTests
    {
        /// <summary>
        /// Verify the worker collects immediately and prunes expired samples.
        /// </summary>
        [Fact]
        public async Task ShouldCollectAndPruneOnStart()
        {
            string tempDirectory = CreateTempDirectory();
            string databaseFile = Path.Combine(tempDirectory, "history.db");
            string logDirectory = Path.Combine(tempDirectory, "logs");
            DateTimeOffset now = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

            PersistenceSettings settings = new PersistenceSettings
            {
                Enabled = true,
                Hostname = "worker-host",
                CollectionIntervalMs = 60000,
                RetentionDays = 1,
                PruneIntervalMinutes = 60,
                Database = new DatabaseSettings
                {
                    Filename = databaseFile
                }
            };

            DatabaseDriverBase database = DatabaseDriverFactory.Create(settings);
            AppLogger logger = new AppLogger(new LoggingSettings
            {
                LogDirectory = logDirectory,
                ConsoleLogging = false,
                FileLogging = true
            });
            ManualTimeProvider timeProvider = new ManualTimeProvider(now);
            FakeTelemetryService telemetryService = new FakeTelemetryService(() => BuildSnapshot(now.UtcDateTime, 42D));
            TelemetryPersistenceService persistenceService = new TelemetryPersistenceService(settings, database, telemetryService, logger, timeProvider);

            try
            {
                await database.InitializeAsync(CancellationToken.None);
                TelemetrySampleDetail expired = await database.TelemetryHistory.CreateAsync(
                    BuildSnapshot(now.UtcDateTime.AddDays(-2), 10D),
                    CancellationToken.None);

                await persistenceService.StartAsync(CancellationToken.None);

                TelemetryHistorySearchResult result = await WaitForWorkerAsync(database);

                Assert.True(telemetryService.SnapshotCount > 0);
                Assert.Equal(1L, result.TotalCount);
                Assert.Equal("worker-host", result.Data[0].Hostname);
                Assert.Equal(42D, result.Data[0].CpuUtilizationPercent);
                Assert.Null(await database.TelemetryHistory.ReadDetailAsync(expired.Id, CancellationToken.None));
                Assert.NotNull(persistenceService.GetStatus().LastSuccessUtc);
            }
            finally
            {
                await persistenceService.StopAsync();
                persistenceService.Dispose();
                database.Dispose();
                DeleteDirectory(tempDirectory);
            }
        }

        private static async Task<TelemetryHistorySearchResult> WaitForWorkerAsync(DatabaseDriverBase database)
        {
            for (int i = 0; i < 50; i++)
            {
                TelemetryHistorySearchResult result = await database.TelemetryHistory.SearchAsync(
                    new TelemetryHistorySearchFilter
                    {
                        Hostname = "worker-host",
                        PageSize = 10
                    },
                    CancellationToken.None);

                if (result.TotalCount == 1L && result.Data[0].CpuUtilizationPercent == 42D)
                {
                    return result;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException("Telemetry persistence worker did not collect and prune within the expected time.");
        }

        private static TelemetrySnapshot BuildSnapshot(DateTime collectedUtc, double cpuPercent)
        {
            return new TelemetrySnapshot
            {
                CollectedUtc = collectedUtc,
                HostPlatform = HostPlatformEnum.Windows,
                Cpu = new CpuTelemetry
                {
                    LogicalCoreCount = 16,
                    UtilizationPercent = cpuPercent
                },
                Memory = new MemoryTelemetry
                {
                    TotalBytes = 1024L,
                    UsedBytes = 512L,
                    AvailableBytes = 512L,
                    UtilizationPercent = 50D
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
