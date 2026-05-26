namespace Test.Xunit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Services;
    using RigMonitor.Telemetry.Services;
    using Test.Shared;

    /// <summary>
    /// Verifies telemetry collection status metadata.
    /// </summary>
    public class TelemetryServiceCollectionStatusTests
    {
        /// <summary>
        /// Verify successful collection records section status, timing, and freshness.
        /// </summary>
        [Fact]
        public async Task ShouldRecordSuccessfulSectionCollectionMetadata()
        {
            ManualTimeProvider timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 05, 26, 18, 00, 00, TimeSpan.Zero));
            TestTelemetryComponents components = CreateComponents();
            TelemetrySettings settings = new TelemetrySettings
            {
                SectionStaleAfterMs = 15000
            };

            components.SystemTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(5), new SystemTelemetry { Hostname = "rig-a" });
            components.CpuTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(6), new CpuTelemetry { LogicalCoreCount = 32, UtilizationPercent = 17.5D });
            components.MemoryTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(7), new MemoryTelemetry { TotalBytes = 1024, AvailableBytes = 256, UsedBytes = 768, UtilizationPercent = 75D });
            components.NetworkTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(8), new NetworkTelemetry { ActiveInterfaceCount = 1, TotalReceiveBytesPerSecond = 42D });
            components.DiskTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(9), new DiskTelemetry { ReadOperationsPerSecond = 11D });
            components.GpuTelemetryHandler = _ => CompleteAfterAsync<GpuTelemetry?>(timeProvider, TimeSpan.FromMilliseconds(10), new GpuTelemetry { Vendor = "NVIDIA", ExporterEndpoint = "http://gpu/metrics" });
            components.OllamaTelemetryHandler = _ => CompleteAfterAsync<OllamaTelemetry?>(timeProvider, TimeSpan.FromMilliseconds(11), new OllamaTelemetry { Available = true, BaseUrl = "http://ollama", Version = "0.9.1" });

            TelemetryService service = CreateService(settings, components, timeProvider);
            TelemetrySnapshot snapshot = await service.GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);

            TelemetryCollectionMetadata collection = Assert.IsType<TelemetryCollectionMetadata>(snapshot.Collection);
            Assert.NotNull(snapshot.System);
            Assert.NotNull(snapshot.Gpu);
            Assert.NotNull(snapshot.Ollama);
            Assert.Equal(settings.SectionStaleAfterMs, collection.StaleAfterMs);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.System.StatusCode);
            Assert.True(collection.System.Requested);
            Assert.True(collection.System.Supported);
            Assert.Equal(TelemetryFreshnessStatusEnum.Fresh, Assert.IsType<TelemetrySectionFreshness>(collection.System.Freshness).Status);
            Assert.Equal(5D, collection.System.LastDurationMs);
            Assert.NotNull(collection.System.LastAttemptUtc);
            Assert.NotNull(collection.System.LastSuccessUtc);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Gpu.StatusCode);
            Assert.Equal(10D, collection.Gpu.LastDurationMs);
            Assert.Equal(TelemetryFreshnessStatusEnum.Fresh, Assert.IsType<TelemetrySectionFreshness>(collection.Gpu.Freshness).Status);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Ollama.StatusCode);
            Assert.Equal(11D, collection.Ollama.LastDurationMs);
            Assert.Equal(TelemetryFreshnessStatusEnum.Fresh, Assert.IsType<TelemetrySectionFreshness>(collection.Ollama.Freshness).Status);
        }

        /// <summary>
        /// Verify selective requests still omit unrequested sections while metadata marks them disabled.
        /// </summary>
        [Fact]
        public async Task ShouldPreserveSelectivePayloadBehaviorAndMarkDisabledSections()
        {
            ManualTimeProvider timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 05, 26, 18, 05, 00, TimeSpan.Zero));
            TestTelemetryComponents components = CreateComponents();
            TelemetrySettings settings = new TelemetrySettings();

            components.CpuTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(5), new CpuTelemetry { LogicalCoreCount = 16, UtilizationPercent = 22D });
            components.MemoryTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(5), new MemoryTelemetry { TotalBytes = 2048, AvailableBytes = 1024, UsedBytes = 1024, UtilizationPercent = 50D });
            components.NetworkTelemetryHandler = _ => CompleteAfterAsync(timeProvider, TimeSpan.FromMilliseconds(5), new NetworkTelemetry { ActiveInterfaceCount = 2 });
            components.GpuTelemetryHandler = _ => Task.FromException<GpuTelemetry?>(new InvalidOperationException("GPU collector should not run when gpu=false."));

            TelemetryService service = CreateService(settings, components, timeProvider);
            TelemetryRequestOptions requestOptions = TelemetryRequestParser.Parse("/v1/telemetry?cpu&memory&network&gpu=false");
            TelemetrySnapshot snapshot = await service.GetSnapshotAsync(requestOptions, CancellationToken.None);

            TelemetryCollectionMetadata collection = Assert.IsType<TelemetryCollectionMetadata>(snapshot.Collection);
            Assert.Null(snapshot.System);
            Assert.NotNull(snapshot.Cpu);
            Assert.NotNull(snapshot.Memory);
            Assert.NotNull(snapshot.Network);
            Assert.Null(snapshot.Disk);
            Assert.Null(snapshot.Gpu);
            Assert.Null(snapshot.Ollama);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Disabled, collection.System.StatusCode);
            Assert.False(collection.System.Requested);
            Assert.Equal(TelemetryFreshnessStatusEnum.NotApplicable, Assert.IsType<TelemetrySectionFreshness>(collection.System.Freshness).Status);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Disabled, collection.Gpu.StatusCode);
            Assert.False(collection.Gpu.Requested);
            Assert.True(collection.Gpu.Supported);
            Assert.Null(collection.Gpu.LastAttemptUtc);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Cpu.StatusCode);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Memory.StatusCode);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Network.StatusCode);
        }

        /// <summary>
        /// Verify requested but unsupported sections are reported explicitly.
        /// </summary>
        [Fact]
        public async Task ShouldMarkRequestedUnsupportedSections()
        {
            ManualTimeProvider timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 05, 26, 18, 10, 00, TimeSpan.Zero));
            TestTelemetryComponents components = CreateComponents();
            TelemetrySettings settings = new TelemetrySettings();
            components.Current.NvidiaAvailable = false;
            components.Current.OllamaAvailable = false;
            components.GpuTelemetryHandler = _ => Task.FromException<GpuTelemetry?>(new InvalidOperationException("GPU collector should not run when unsupported."));
            components.OllamaTelemetryHandler = _ => Task.FromException<OllamaTelemetry?>(new InvalidOperationException("Ollama collector should not run when unsupported."));

            TelemetryService service = CreateService(settings, components, timeProvider);
            TelemetrySnapshot snapshot = await service.GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);

            TelemetryCollectionMetadata collection = Assert.IsType<TelemetryCollectionMetadata>(snapshot.Collection);
            Assert.Null(snapshot.Gpu);
            Assert.Null(snapshot.Ollama);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Unsupported, collection.Gpu.StatusCode);
            Assert.True(collection.Gpu.Requested);
            Assert.False(collection.Gpu.Supported);
            Assert.Null(collection.Gpu.LastAttemptUtc);

            Assert.Equal(TelemetryCollectionStatusCodeEnum.Unsupported, collection.Ollama.StatusCode);
            Assert.True(collection.Ollama.Requested);
            Assert.False(collection.Ollama.Supported);
            Assert.Null(collection.Ollama.LastAttemptUtc);
        }

        /// <summary>
        /// Verify a supported section that returns no sample is marked unavailable.
        /// </summary>
        [Fact]
        public async Task ShouldMarkUnavailableSectionsWhenNoCurrentSampleExists()
        {
            ManualTimeProvider timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 05, 26, 18, 15, 00, TimeSpan.Zero));
            TestTelemetryComponents components = CreateComponents();
            TelemetrySettings settings = new TelemetrySettings
            {
                SectionStaleAfterMs = 5000
            };
            components.GpuTelemetryHandler = _ => CompleteAfterAsync<GpuTelemetry?>(timeProvider, TimeSpan.FromMilliseconds(12), null);

            TelemetryService service = CreateService(settings, components, timeProvider);
            TelemetryRequestOptions requestOptions = TelemetryRequestOptions.None();
            requestOptions.IncludeGpu = true;

            TelemetrySnapshot snapshot = await service.GetSnapshotAsync(requestOptions, CancellationToken.None);

            TelemetryCollectionMetadata collection = Assert.IsType<TelemetryCollectionMetadata>(snapshot.Collection);
            Assert.Null(snapshot.Gpu);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Unavailable, collection.Gpu.StatusCode);
            Assert.Equal(TelemetryFreshnessStatusEnum.Unknown, Assert.IsType<TelemetrySectionFreshness>(collection.Gpu.Freshness).Status);
            Assert.NotNull(collection.Gpu.LastAttemptUtc);
            Assert.Null(collection.Gpu.LastSuccessUtc);
            Assert.Equal(12D, collection.Gpu.LastDurationMs);
        }

        /// <summary>
        /// Verify stale status is reported after a previous success ages out and a later request fails.
        /// </summary>
        [Fact]
        public async Task ShouldMarkSectionsAsStaleAfterPreviousSuccessExpires()
        {
            ManualTimeProvider timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 05, 26, 18, 20, 00, TimeSpan.Zero));
            TestTelemetryComponents components = CreateComponents();
            TelemetrySettings settings = new TelemetrySettings
            {
                SectionStaleAfterMs = 1000
            };

            TelemetryService service = CreateService(settings, components, timeProvider);
            TelemetryRequestOptions requestOptions = TelemetryRequestOptions.None();
            requestOptions.IncludeGpu = true;

            components.GpuTelemetryHandler = _ => CompleteAfterAsync<GpuTelemetry?>(timeProvider, TimeSpan.FromMilliseconds(15), new GpuTelemetry { Vendor = "NVIDIA" });
            TelemetrySnapshot firstSnapshot = await service.GetSnapshotAsync(requestOptions, CancellationToken.None);
            DateTime? firstSuccessUtc = Assert.IsType<TelemetryCollectionMetadata>(firstSnapshot.Collection).Gpu.LastSuccessUtc;

            timeProvider.Advance(TimeSpan.FromMilliseconds(1500));
            components.GpuTelemetryHandler = _ => CompleteAfterAsync<GpuTelemetry?>(timeProvider, TimeSpan.FromMilliseconds(20), null);

            TelemetrySnapshot secondSnapshot = await service.GetSnapshotAsync(requestOptions, CancellationToken.None);

            TelemetryCollectionMetadata collection = Assert.IsType<TelemetryCollectionMetadata>(secondSnapshot.Collection);
            TelemetrySectionFreshness freshness = Assert.IsType<TelemetrySectionFreshness>(collection.Gpu.Freshness);
            Assert.Null(secondSnapshot.Gpu);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Stale, collection.Gpu.StatusCode);
            Assert.Equal(TelemetryFreshnessStatusEnum.Stale, freshness.Status);
            Assert.True(freshness.AgeMs >= 1500D);
            Assert.Equal(firstSuccessUtc, collection.Gpu.LastSuccessUtc);
            Assert.NotNull(collection.Gpu.LastAttemptUtc);
            Assert.Equal(20D, collection.Gpu.LastDurationMs);
        }

        /// <summary>
        /// Verify collector exceptions are surfaced through error metadata.
        /// </summary>
        [Fact]
        public async Task ShouldMarkSectionsAsErrorWhenCollectorThrows()
        {
            ManualTimeProvider timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 05, 26, 18, 25, 00, TimeSpan.Zero));
            TestTelemetryComponents components = CreateComponents();
            TelemetrySettings settings = new TelemetrySettings();
            components.CpuTelemetryHandler = _ => ThrowAfterAsync<CpuTelemetry>(timeProvider, TimeSpan.FromMilliseconds(9), new InvalidOperationException("CPU sampler failed."));

            TelemetryService service = CreateService(settings, components, timeProvider);
            TelemetryRequestOptions requestOptions = TelemetryRequestOptions.None();
            requestOptions.IncludeCpu = true;

            TelemetrySnapshot snapshot = await service.GetSnapshotAsync(requestOptions, CancellationToken.None);

            TelemetryCollectionMetadata collection = Assert.IsType<TelemetryCollectionMetadata>(snapshot.Collection);
            Assert.Null(snapshot.Cpu);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Error, collection.Cpu.StatusCode);
            Assert.Equal(TelemetryFreshnessStatusEnum.Unknown, Assert.IsType<TelemetrySectionFreshness>(collection.Cpu.Freshness).Status);
            Assert.Equal("CPU sampler failed.", collection.Cpu.LastError);
            Assert.NotNull(collection.Cpu.LastAttemptUtc);
            Assert.Null(collection.Cpu.LastSuccessUtc);
            Assert.Equal(9D, collection.Cpu.LastDurationMs);
        }

        private static TelemetryService CreateService(TelemetrySettings settings, TestTelemetryComponents components, ManualTimeProvider timeProvider)
        {
            return new TelemetryService(
                settings,
                components,
                components,
                components,
                components,
                components,
                components,
                timeProvider);
        }

        private static TestTelemetryComponents CreateComponents()
        {
            return new TestTelemetryComponents
            {
                Current = new RuntimeCapabilities
                {
                    HostPlatform = HostPlatformEnum.Windows,
                    NvidiaAvailable = true,
                    OllamaAvailable = true
                }
            };
        }

        private static Task<T> CompleteAfterAsync<T>(ManualTimeProvider timeProvider, TimeSpan delay, T value)
        {
            timeProvider.Advance(delay);
            return Task.FromResult(value);
        }

        private static Task<T> ThrowAfterAsync<T>(ManualTimeProvider timeProvider, TimeSpan delay, Exception exception)
        {
            timeProvider.Advance(delay);
            return Task.FromException<T>(exception);
        }
    }
}
