namespace RigMonitor.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Telemetry.Platform.Shared;

    /// <summary>
    /// Default network throughput sampler.
    /// </summary>
    public class DefaultNetworkRateSampler : INetworkRateSampler
    {
        private readonly Dictionary<string, NetworkInterfaceSample> _PreviousSamples = new Dictionary<string, NetworkInterfaceSample>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Warm the sampler with an initial sample.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task WarmupAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, NetworkInterfaceSample> samples = CaptureCurrentSamples();
            _PreviousSamples.Clear();

            foreach (KeyValuePair<string, NetworkInterfaceSample> sample in samples)
            {
                _PreviousSamples[sample.Key] = sample.Value;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Capture a current network sample.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Network telemetry.</returns>
        public Task<NetworkTelemetry> CaptureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NetworkTelemetry telemetry = new NetworkTelemetry();
            Dictionary<string, NetworkInterfaceSample> currentSamples = new Dictionary<string, NetworkInterfaceSample>(StringComparer.OrdinalIgnoreCase);
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface networkInterface in interfaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IPInterfaceStatistics statistics = networkInterface.GetIPStatistics();
                DateTime capturedUtc = DateTime.UtcNow;
                NetworkInterfaceTelemetry interfaceTelemetry = new NetworkInterfaceTelemetry
                {
                    Name = networkInterface.Name,
                    Description = networkInterface.Description,
                    Type = networkInterface.NetworkInterfaceType,
                    OperationalStatus = networkInterface.OperationalStatus,
                    MacAddress = networkInterface.GetPhysicalAddress().ToString(),
                    UnicastAddresses = networkInterface
                        .GetIPProperties()
                        .UnicastAddresses
                        .Where(node => node.Address.AddressFamily == AddressFamily.InterNetwork || node.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        .Select(node => node.Address.ToString())
                        .ToList(),
                    BytesReceivedTotal = statistics.BytesReceived,
                    BytesSentTotal = statistics.BytesSent
                };

                if (_PreviousSamples.TryGetValue(networkInterface.Id, out NetworkInterfaceSample? previous))
                {
                    double elapsedSeconds = Math.Max(0.001D, (capturedUtc - previous.CapturedUtc).TotalSeconds);
                    long receivedDelta = Math.Max(0L, interfaceTelemetry.BytesReceivedTotal - previous.BytesReceived);
                    long sentDelta = Math.Max(0L, interfaceTelemetry.BytesSentTotal - previous.BytesSent);
                    interfaceTelemetry.ReceiveBytesPerSecond = receivedDelta / elapsedSeconds;
                    interfaceTelemetry.TransmitBytesPerSecond = sentDelta / elapsedSeconds;
                }

                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    telemetry.ActiveInterfaceCount += 1;
                }

                telemetry.TotalReceiveBytesPerSecond += interfaceTelemetry.ReceiveBytesPerSecond;
                telemetry.TotalTransmitBytesPerSecond += interfaceTelemetry.TransmitBytesPerSecond;
                telemetry.Interfaces.Add(interfaceTelemetry);
                currentSamples[networkInterface.Id] = new NetworkInterfaceSample
                {
                    CapturedUtc = capturedUtc,
                    BytesReceived = interfaceTelemetry.BytesReceivedTotal,
                    BytesSent = interfaceTelemetry.BytesSentTotal
                };
            }

            _PreviousSamples.Clear();
            foreach (KeyValuePair<string, NetworkInterfaceSample> sample in currentSamples)
            {
                _PreviousSamples[sample.Key] = sample.Value;
            }

            return Task.FromResult(telemetry);
        }

        private static Dictionary<string, NetworkInterfaceSample> CaptureCurrentSamples()
        {
            Dictionary<string, NetworkInterfaceSample> samples = new Dictionary<string, NetworkInterfaceSample>(StringComparer.OrdinalIgnoreCase);
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface networkInterface in interfaces)
            {
                IPInterfaceStatistics statistics = networkInterface.GetIPStatistics();
                samples[networkInterface.Id] = new NetworkInterfaceSample
                {
                    CapturedUtc = DateTime.UtcNow,
                    BytesReceived = statistics.BytesReceived,
                    BytesSent = statistics.BytesSent
                };
            }

            return samples;
        }
    }
}
