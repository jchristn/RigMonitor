namespace Test.Xunit
{
    using System.IO;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;
    using RigMonitor.Server.Serialization;

    /// <summary>
    /// JSON serialization behavior tests.
    /// </summary>
    public class JsonSerializationTests
    {
        /// <summary>
        /// Verify enums are serialized as camelCase strings.
        /// </summary>
        [Fact]
        public void ShouldSerializeEnumValuesAsCamelCase()
        {
            TelemetrySnapshot snapshot = new TelemetrySnapshot
            {
                HostPlatform = HostPlatformEnum.Windows,
                System = new SystemTelemetry
                {
                    Hostname = "example-host",
                    UptimeMs = 1234,
                    OsDescription = "Example OS",
                    OsArchitecture = Architecture.X64,
                    ProcessArchitecture = Architecture.Arm64
                },
                Network = new NetworkTelemetry
                {
                    Interfaces =
                    {
                        new NetworkInterfaceTelemetry
                        {
                            Name = "ethernet0",
                            Type = NetworkInterfaceType.Wireless80211,
                            OperationalStatus = OperationalStatus.Up
                        }
                    }
                },
                Disk = new DiskTelemetry
                {
                    Volumes =
                    {
                        new DiskVolumeTelemetry
                        {
                            Name = "C:\\",
                            MountPoint = "C:\\",
                            DriveType = DriveType.Fixed
                        }
                    }
                },
                Collection = new TelemetryCollectionMetadata
                {
                    System = new TelemetrySectionCollectionStatus
                    {
                        Requested = true,
                        Supported = true,
                        StatusCode = TelemetryCollectionStatusCodeEnum.Stale,
                        Freshness = new TelemetrySectionFreshness
                        {
                            Status = TelemetryFreshnessStatusEnum.Stale,
                            AgeMs = 30000D,
                            StaleAfterMs = 15000
                        }
                    }
                }
            };

            string json = RigMonitorJsonSerializer.Serialize(snapshot);

            Assert.Contains("\"hostPlatform\":\"windows\"", json);
            Assert.Contains("\"osArchitecture\":\"x64\"", json);
            Assert.Contains("\"processArchitecture\":\"arm64\"", json);
            Assert.Contains("\"type\":\"wireless80211\"", json);
            Assert.Contains("\"operationalStatus\":\"up\"", json);
            Assert.Contains("\"driveType\":\"fixed\"", json);
            Assert.Contains("\"collection\":", json);
            Assert.Contains("\"statusCode\":\"stale\"", json);
            Assert.Contains("\"freshness\":{\"status\":\"stale\"", json);
        }
    }
}
