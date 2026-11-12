namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Observability;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Serialization;
    using RigMonitor.Server.Services;
    using RigMonitor.Telemetry.Gpu;
    using RigMonitor.Telemetry.Gpu.Prometheus;
    using RigMonitor.Telemetry.Services;
    using Test.Shared;

    /// <summary>
    /// Cross-vendor behavior: Prometheus parsing, the gpu aggregate, vendor sections in snapshots, derived status, capabilities,
    /// selectors, serialization, and settings.
    /// </summary>
    [Collection(TelemetryTestCollection.Name)]
    public class GpuVendorSectionTests
    {
        /// <summary>
        /// The Prometheus parser handles escaped labels, comments, timestamps, special values, and unlabeled metrics.
        /// </summary>
        [Fact]
        public void PrometheusParserShouldHandleExpositionEdgeCases()
        {
            string text =
                "# HELP x help\r\n" +
                "# TYPE x gauge\r\n" +
                "\r\n" +
                "metric_a{name=\"a, \\\"quoted\\\" value\",other=\"b\"} 1.5e2 1700000000000\r\n" +
                "metric_b 42\n" +
                "metric_nan{gpu=\"0\"} NaN\n" +
                "metric_inf{gpu=\"0\"} +Inf\n" +
                "metric_ninf -Inf\n" +
                "broken_line\n" +
                "metric_bad{gpu=\"0\"} notanumber\n";

            List<PrometheusSample> samples = PrometheusTextParser.Parse(text);

            Assert.Equal(5, samples.Count);
            Assert.Equal("metric_a", samples[0].Name);
            Assert.Equal(150D, samples[0].Value);
            Assert.Equal("a, \"quoted\" value", samples[0].Labels["name"]);
            Assert.Equal("b", samples[0].Label("missing", "OTHER"));
            Assert.Equal(42D, samples[1].Value);
            Assert.Empty(samples[1].Labels);
            Assert.True(Double.IsNaN(samples[2].Value));
            Assert.True(Double.IsPositiveInfinity(samples[3].Value));
            Assert.True(Double.IsNegativeInfinity(samples[4].Value));
            Assert.Empty(PrometheusTextParser.Parse(null));
        }

        /// <summary>
        /// The aggregate orders devices by vendor, assigns ordinals, and reports mixed hosts.
        /// </summary>
        [Fact]
        public void AggregatorShouldOrderAndLabelVendors()
        {
            NvidiaGpuTelemetry nvidia = new NvidiaGpuTelemetry { Devices = { new NvidiaGpuDeviceTelemetry { DeviceIndex = 1 }, new NvidiaGpuDeviceTelemetry { DeviceIndex = 0 } } };
            AmdGpuTelemetry amd = new AmdGpuTelemetry { Devices = { new AmdGpuDeviceTelemetry { DeviceIndex = 0 } } };

            GpuTelemetry? mixed = GpuAggregator.Build(nvidia, amd, null, null);

            Assert.NotNull(mixed);
            Assert.Equal(GpuVendorEnum.Mixed, mixed.Vendor);
            Assert.Equal(new[] { GpuVendorEnum.Nvidia, GpuVendorEnum.Amd }, mixed.Vendors);
            Assert.Equal(new[] { 0, 1, 2 }, mixed.Devices.Select(device => device.Ordinal));
            Assert.Equal(new[] { 0, 1, 0 }, mixed.Devices.Select(device => device.DeviceIndex));

            GpuTelemetry? apple = GpuAggregator.Build(null, null, new AppleGpuTelemetry { Devices = { new AppleGpuDeviceTelemetry() } }, null);
            Assert.Equal(GpuVendorEnum.Apple, apple!.Vendor);
            Assert.Null(GpuAggregator.Build(null, new AmdGpuTelemetry(), null, null));
        }

        /// <summary>
        /// A full snapshot includes every available vendor section, the aggregate, and a derived gpu status.
        /// </summary>
        [Fact]
        public async Task SnapshotShouldIncludeVendorSectionsAndAggregate()
        {
            TestTelemetryComponents components = CreateComponents(nvidia: true, amd: true, apple: false, qualcomm: true);
            components.NvidiaTelemetryHandler = _ => Task.FromResult<NvidiaGpuTelemetry?>(new NvidiaGpuTelemetry { Devices = { new NvidiaGpuDeviceTelemetry { Model = "NVIDIA L4" } } });
            components.AmdTelemetryHandler = _ => Task.FromResult<AmdGpuTelemetry?>(new AmdGpuTelemetry { Source = GpuTelemetrySourceEnum.AmdSysfs, Devices = { new AmdGpuDeviceTelemetry { Model = "AMD Radeon RX 7900 XTX" } } });
            components.QualcommTelemetryHandler = _ => Task.FromException<QualcommGpuTelemetry?>(new InvalidOperationException("adreno failed"));

            TelemetrySnapshot snapshot = await CreateService(components).GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);

            Assert.True(snapshot.NvidiaAvailable);
            Assert.True(snapshot.AmdAvailable);
            Assert.False(snapshot.AppleGpuAvailable);
            Assert.True(snapshot.QualcommAvailable);
            Assert.NotNull(snapshot.Nvidia);
            Assert.NotNull(snapshot.Amd);
            Assert.Null(snapshot.Apple);
            Assert.Null(snapshot.Qualcomm);
            Assert.Equal(GpuVendorEnum.Mixed, snapshot.Gpu!.Vendor);
            Assert.Equal(2, snapshot.Gpu.Devices.Count);

            TelemetryCollectionMetadata collection = snapshot.Collection!;
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Nvidia.StatusCode);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Amd.StatusCode);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Unsupported, collection.Apple.StatusCode);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Error, collection.Qualcomm.StatusCode);
            Assert.Equal("adreno failed", collection.Qualcomm.LastError);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, collection.Gpu.StatusCode);
            Assert.True(collection.Gpu.Supported);
            Assert.Contains("qualcomm: error", collection.Gpu.Message);
            Assert.Contains("adreno failed", collection.Gpu.LastError);
        }

        /// <summary>
        /// The derived gpu status takes the most severe vendor status when no vendor succeeded.
        /// </summary>
        [Fact]
        public async Task DerivedStatusShouldUseMostSevereVendorWhenNoneSucceed()
        {
            TestTelemetryComponents components = CreateComponents(nvidia: true, amd: true, apple: false, qualcomm: false);
            components.NvidiaTelemetryHandler = _ => Task.FromResult<NvidiaGpuTelemetry?>(null);
            components.AmdTelemetryHandler = _ => Task.FromException<AmdGpuTelemetry?>(new InvalidOperationException("amd failed"));

            TelemetryRequestOptions options = TelemetryRequestOptions.None();
            options.IncludeGpu = true;
            TelemetrySnapshot snapshot = await CreateService(components).GetSnapshotAsync(options, CancellationToken.None);

            Assert.Null(snapshot.Gpu);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Error, snapshot.Collection!.Gpu.StatusCode);
            Assert.Contains("nvidia: unavailable", snapshot.Collection.Gpu.Message);
            Assert.Contains("amd: error", snapshot.Collection.Gpu.Message);
        }

        /// <summary>
        /// Selectors: gpu alone collects vendors internally but emits only the aggregate; a vendor key emits only that section.
        /// </summary>
        [Fact]
        public async Task SelectorsShouldControlEmittedSections()
        {
            TestTelemetryComponents components = CreateComponents(nvidia: true, amd: true, apple: false, qualcomm: false);
            int amdCalls = 0;
            components.NvidiaTelemetryHandler = _ => Task.FromResult<NvidiaGpuTelemetry?>(new NvidiaGpuTelemetry { Devices = { new NvidiaGpuDeviceTelemetry() } });
            components.AmdTelemetryHandler = _ =>
            {
                amdCalls++;
                return Task.FromResult<AmdGpuTelemetry?>(new AmdGpuTelemetry { Devices = { new AmdGpuDeviceTelemetry() } });
            };
            TelemetryService service = CreateService(components);

            TelemetrySnapshot gpuOnly = await service.GetSnapshotAsync(TelemetryRequestParser.Parse("/v1/telemetry?gpu"), CancellationToken.None);
            Assert.NotNull(gpuOnly.Gpu);
            Assert.Null(gpuOnly.Nvidia);
            Assert.Null(gpuOnly.Amd);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Disabled, gpuOnly.Collection!.Nvidia.StatusCode);
            Assert.Equal(1, amdCalls);

            TelemetrySnapshot amdOnly = await service.GetSnapshotAsync(TelemetryRequestParser.Parse("/v1/telemetry?amd"), CancellationToken.None);
            Assert.Null(amdOnly.Gpu);
            Assert.NotNull(amdOnly.Amd);
            Assert.Null(amdOnly.Nvidia);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Disabled, amdOnly.Collection!.Gpu.StatusCode);
            Assert.Equal(TelemetryCollectionStatusCodeEnum.Ok, amdOnly.Collection.Amd.StatusCode);

            TelemetrySnapshot noGpu = await service.GetSnapshotAsync(TelemetryRequestParser.Parse("/v1/telemetry?cpu"), CancellationToken.None);
            Assert.Null(noGpu.Gpu);
            Assert.Null(noGpu.Amd);
            Assert.Equal(2, amdCalls);
        }

        /// <summary>
        /// Vendor sections are collected concurrently.
        /// </summary>
        [Fact]
        public async Task VendorSectionsShouldCollectInParallel()
        {
            TestTelemetryComponents components = CreateComponents(nvidia: true, amd: true, apple: true, qualcomm: true);
            components.NvidiaTelemetryHandler = async token => { await Task.Delay(400, token); return new NvidiaGpuTelemetry { Devices = { new NvidiaGpuDeviceTelemetry() } }; };
            components.AmdTelemetryHandler = async token => { await Task.Delay(400, token); return new AmdGpuTelemetry { Devices = { new AmdGpuDeviceTelemetry() } }; };
            components.AppleTelemetryHandler = async token => { await Task.Delay(400, token); return new AppleGpuTelemetry { Devices = { new AppleGpuDeviceTelemetry() } }; };
            components.QualcommTelemetryHandler = async token => { await Task.Delay(400, token); return new QualcommGpuTelemetry { Devices = { new QualcommGpuDeviceTelemetry() } }; };

            TelemetryRequestOptions options = TelemetryRequestOptions.None();
            options.IncludeGpu = true;
            Stopwatch stopwatch = Stopwatch.StartNew();
            TelemetrySnapshot snapshot = await CreateService(components).GetSnapshotAsync(options, CancellationToken.None);
            stopwatch.Stop();

            Assert.Equal(4, snapshot.Gpu!.Devices.Count);
            Assert.True(stopwatch.ElapsedMilliseconds < 1200, "Vendor collection took " + stopwatch.ElapsedMilliseconds + " ms.");
        }

        /// <summary>
        /// Unified-memory devices that report their own usage keep it and take capacity from the host; others take both.
        /// </summary>
        [Fact]
        public async Task UnifiedMemoryFallbackShouldKeepReportedUsage()
        {
            TestTelemetryComponents components = CreateComponents(nvidia: false, amd: false, apple: false, qualcomm: true);
            components.MemoryTelemetryHandler = _ => Task.FromResult(new MemoryTelemetry
            {
                TotalBytes = 32L * 1024L * 1024L * 1024L,
                AvailableBytes = 24L * 1024L * 1024L * 1024L,
                UsedBytes = 8L * 1024L * 1024L * 1024L,
                UtilizationPercent = 25D
            });
            QualcommGpuDeviceTelemetry reporting = new QualcommGpuDeviceTelemetry { DeviceIndex = 0 };
            reporting.Metrics.MemoryUsedMegabytes = 512D;
            QualcommGpuDeviceTelemetry silent = new QualcommGpuDeviceTelemetry { DeviceIndex = 1 };
            components.QualcommTelemetryHandler = _ => Task.FromResult<QualcommGpuTelemetry?>(new QualcommGpuTelemetry { Devices = { reporting, silent } });

            TelemetrySnapshot snapshot = await CreateService(components).GetSnapshotAsync(TelemetryRequestOptions.All(), CancellationToken.None);

            QualcommGpuDeviceTelemetry first = snapshot.Qualcomm!.Devices[0];
            Assert.Equal(512D, first.Metrics.MemoryUsedMegabytes);
            Assert.Equal(32768D, first.Metrics.MemoryTotalMegabytes);
            Assert.Equal("unifiedSystemMemory", first.Metrics.MemorySource);
            QualcommGpuDeviceTelemetry second = snapshot.Qualcomm.Devices[1];
            Assert.Equal(8192D, second.Metrics.MemoryUsedMegabytes);
            Assert.Equal(32768D, second.Metrics.MemoryTotalMegabytes);
            Assert.Same(first, snapshot.Gpu!.Devices[0]);
        }

        /// <summary>
        /// Gpu collection emits a stage:gpu span parenting the vendor stage spans and a derived gpu section metric.
        /// </summary>
        [Fact]
        public async Task GpuStageSpanShouldParentVendorSpans()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TestTelemetryComponents components = CreateComponents(nvidia: true, amd: false, apple: false, qualcomm: false);
                components.NvidiaTelemetryHandler = _ => Task.FromResult<NvidiaGpuTelemetry?>(new NvidiaGpuTelemetry { Devices = { new NvidiaGpuDeviceTelemetry() } });
                TelemetryRequestOptions options = TelemetryRequestOptions.None();
                options.IncludeGpu = true;

                await CreateService(components).GetSnapshotAsync(options, CancellationToken.None);

                Activity gpu = Assert.Single(capture.Spans("stage:gpu"));
                Activity nvidia = Assert.Single(capture.Spans("stage:nvidia"));
                Assert.Equal(gpu.SpanId, nvidia.ParentSpanId);
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "gpu", RigMonitorTelemetryNames.LabelSectionStatus, "ok"));
                Assert.Equal(1D, capture.Sum(RigMonitorTelemetryNames.SectionCollections, RigMonitorTelemetryNames.LabelSection, "amd", RigMonitorTelemetryNames.LabelSectionStatus, "unsupported"));
            }
        }

        /// <summary>
        /// Capability probing runs every vendor, records sources and features, and turns probe exceptions into unavailable.
        /// </summary>
        [Fact]
        public async Task CapabilitiesShouldAggregateVendorProbes()
        {
            using (TelemetryCapture capture = new TelemetryCapture())
            {
                TestTelemetryComponents components = new TestTelemetryComponents();
                components.GpuProbeHandler = (vendor, token) =>
                {
                    switch (vendor)
                    {
                        case GpuVendorEnum.Amd:
                            return Task.FromResult(new GpuProbeResult { Available = true, Source = GpuTelemetrySourceEnum.AmdSmiLibrary, Detail = "lib 6.4.0" });
                        case GpuVendorEnum.Apple:
                            return Task.FromResult(new GpuProbeResult { Available = true, Source = GpuTelemetrySourceEnum.AppleNative, Features = { "ioKit", "metal" } });
                        case GpuVendorEnum.Qualcomm:
                            return Task.FromException<GpuProbeResult>(new InvalidOperationException("probe crashed"));
                        default:
                            return Task.FromResult(new GpuProbeResult { Available = false, Detail = "no exporter" });
                    }
                };

                TelemetrySettings settings = new TelemetrySettings();
                RuntimeCapabilitiesService service = new RuntimeCapabilitiesService(settings, true, components, components, components, components, components, components, components);
                await service.InitializeAsync(CancellationToken.None);

                RuntimeCapabilities current = service.Current;
                Assert.False(current.NvidiaAvailable);
                Assert.True(current.AmdAvailable);
                Assert.True(current.AppleGpuAvailable);
                Assert.False(current.QualcommAvailable);
                Assert.True(current.GpuAvailable);
                Assert.Equal(GpuTelemetrySourceEnum.AmdSmiLibrary, current.AmdSource);
                Assert.Null(current.QualcommSource);
                Assert.Equal(new[] { "ioKit", "metal" }, current.AppleFeatures);
                Assert.Equal(4, current.GpuProviders.Count);
                Assert.Equal(GpuVendorEnum.Qualcomm, current.GpuProviders[3].Vendor);
                Assert.Equal("probe crashed", current.GpuProviders[3].Detail);
                Assert.Equal(settings.AmdMetricsExporterUrl, current.AmdMetricsExporterUrl);

                capture.CollectObservables();
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.CapabilityAvailable, RigMonitorTelemetryNames.LabelCapability, "amd"), m => m.Value == 1D);
                Assert.Contains(capture.Find(RigMonitorTelemetryNames.CapabilityAvailable, RigMonitorTelemetryNames.LabelCapability, "qualcomm"), m => m.Value == 0D);
            }
        }

        /// <summary>
        /// Vendor selector keys parse like the other selectors.
        /// </summary>
        [Fact]
        public void RequestParserShouldRecognizeVendorKeys()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?amd&apple=false&qualcomm");
            Assert.True(options.IncludeAmd);
            Assert.False(options.IncludeApple);
            Assert.True(options.IncludeQualcomm);
            Assert.False(options.IncludeNvidia);
            Assert.False(options.IncludeGpu);
            Assert.False(options.IncludeCpu);

            TelemetryRequestOptions all = TelemetryRequestParser.Parse("/v1/telemetry");
            Assert.True(all.IncludeGpu && all.IncludeNvidia && all.IncludeAmd && all.IncludeApple && all.IncludeQualcomm);

            TelemetryRequestOptions gpu = TelemetryRequestParser.Parse("/v1/telemetry?gpu");
            Assert.True(gpu.IncludeGpu);
            Assert.False(gpu.IncludeNvidia);
        }

        /// <summary>
        /// Vendor enums serialize camelCase, nulls are omitted, and the aggregate carries only common device fields.
        /// </summary>
        [Fact]
        public void SerializationShouldUseCamelCaseAndCommonAggregateShape()
        {
            NvidiaGpuDeviceTelemetry device = new NvidiaGpuDeviceTelemetry { Model = "NVIDIA H100", XidErrors = 3 };
            TelemetrySnapshot snapshot = new TelemetrySnapshot
            {
                Nvidia = new NvidiaGpuTelemetry { Devices = { device } },
                Apple = new AppleGpuTelemetry { ThermalState = GpuThermalStateEnum.Serious, Devices = { new AppleGpuDeviceTelemetry() } },
                Gpu = GpuAggregator.Build(new NvidiaGpuTelemetry { Devices = { device } }, null, null, null)
            };

            string json = RigMonitorJsonSerializer.Serialize(snapshot);
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.Equal("nvidia", root.GetProperty("gpu").GetProperty("vendor").GetString());
                JsonElement aggregateDevice = root.GetProperty("gpu").GetProperty("devices")[0];
                Assert.False(aggregateDevice.TryGetProperty("xidErrors", out _));
                Assert.False(aggregateDevice.GetProperty("metrics").TryGetProperty("temperatureCelsius", out _));
                Assert.Equal(3, root.GetProperty("nvidia").GetProperty("devices")[0].GetProperty("xidErrors").GetInt32());
                Assert.Equal("dcgmExporter", root.GetProperty("nvidia").GetProperty("source").GetString());
                Assert.Equal("serious", root.GetProperty("apple").GetProperty("thermalState").GetString());
                Assert.Equal("appleNative", root.GetProperty("apple").GetProperty("devices")[0].GetProperty("source").GetString());
            }
        }

        /// <summary>
        /// Snapshots stored by 0.2.x (uppercase vendor, metrics.xidErrors, exporterEndpoint) still deserialize.
        /// </summary>
        [Fact]
        public void LegacySnapshotJsonShouldDeserialize()
        {
            const string legacy = "{\"gpu\":{\"vendor\":\"NVIDIA\",\"exporterEndpoint\":\"http://localhost:9400/metrics\",\"devices\":[{\"deviceIndex\":0,\"uuid\":\"GPU-1\",\"model\":\"NVIDIA RTX 4090\",\"migProfile\":null,\"metrics\":{\"gpuUtilizationPercent\":40,\"temperatureCelsius\":61,\"powerUsageWatts\":250,\"xidErrors\":0}}]}}";

            TelemetrySnapshot? snapshot = RigMonitorJsonSerializer.Deserialize<TelemetrySnapshot>(legacy);

            Assert.NotNull(snapshot);
            Assert.Equal(GpuVendorEnum.Nvidia, snapshot.Gpu!.Vendor);
            GpuDeviceTelemetry device = Assert.Single(snapshot.Gpu.Devices);
            Assert.Equal("GPU-1", device.Uuid);
            Assert.Equal(61D, device.Metrics.TemperatureCelsius);
            Assert.Equal(250D, device.Metrics.PowerUsageWatts);
        }

        /// <summary>
        /// GPU settings have documented defaults, clamps, and source normalization.
        /// </summary>
        [Fact]
        public void GpuSettingsShouldDefaultClampAndNormalize()
        {
            TelemetrySettings settings = new TelemetrySettings();
            Assert.True(settings.NvidiaEnabled);
            Assert.True(settings.AmdEnabled);
            Assert.Equal("auto", settings.AmdSource);
            Assert.True(settings.AmdSysfsGapFillEnabled);
            Assert.Equal("/sys/class/drm", settings.AmdSysfsRoot);
            Assert.Equal("http://localhost:5000/metrics", settings.AmdMetricsExporterUrl);
            Assert.True(settings.AppleGpuEnabled);
            Assert.True(settings.AppleIOReportEnabled);
            Assert.True(settings.AppleHidSensorsEnabled);
            Assert.Equal(1000, settings.AppleSamplerIntervalMs);
            Assert.True(settings.QualcommEnabled);
            Assert.Equal("auto", settings.QualcommSource);
            Assert.True(settings.WindowsGenericGpuEnabled);
            Assert.Equal(1000, settings.WindowsGpuSamplerIntervalMs);

            settings.AppleSamplerIntervalMs = 10;
            settings.WindowsGpuSamplerIntervalMs = 999999;
            settings.AmdSource = "SYSFS";
            settings.QualcommSource = "bogus";
            Assert.Equal(250, settings.AppleSamplerIntervalMs);
            Assert.Equal(10000, settings.WindowsGpuSamplerIntervalMs);
            Assert.Equal("sysfs", settings.AmdSource);
            Assert.Equal("auto", settings.QualcommSource);
            settings.QualcommSource = "drmmsm";
            Assert.Equal("drmMsm", settings.QualcommSource);
        }

        /// <summary>
        /// A rewritten settings file gains the GPU keys without losing configured values.
        /// </summary>
        [Fact]
        public async Task SettingsFileShouldGainGpuKeys()
        {
            string directory = Path.Combine(Path.GetTempPath(), "RigMonitorTests", Guid.NewGuid().ToString("N"));
            string file = Path.Combine(directory, "rigmonitor.json");
            Directory.CreateDirectory(directory);
            try
            {
                await File.WriteAllTextAsync(file, "{\"telemetry\":{\"dcgmExporterUrl\":\"http://gpu-host:9400/metrics\"}}");
                RigMonitorSettings settings = await SettingsManager.LoadAsync(file, CancellationToken.None);
                string rewritten = await File.ReadAllTextAsync(file);

                Assert.Equal("http://gpu-host:9400/metrics", settings.Telemetry.DcgmExporterUrl);
                Assert.Contains("\"amdSource\": \"auto\"", rewritten);
                Assert.Contains("\"appleIOReportEnabled\": true", rewritten);
                Assert.Contains("\"qualcommSource\": \"auto\"", rewritten);
                Assert.Contains("\"hideUnsupportedGpuSections\": false", rewritten);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static TestTelemetryComponents CreateComponents(bool nvidia, bool amd, bool apple, bool qualcomm)
        {
            return new TestTelemetryComponents
            {
                Current = new RuntimeCapabilities
                {
                    HostPlatform = HostPlatformEnum.Linux,
                    NvidiaAvailable = nvidia,
                    AmdAvailable = amd,
                    AppleGpuAvailable = apple,
                    QualcommAvailable = qualcomm
                }
            };
        }

        private static TelemetryService CreateService(TestTelemetryComponents components)
        {
            return new TelemetryService(
                new TelemetrySettings(),
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components,
                components);
        }
    }
}
