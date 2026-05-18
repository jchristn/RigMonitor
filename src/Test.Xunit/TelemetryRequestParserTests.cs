namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Server;
    using RigMonitor.Server.Services;

    /// <summary>
    /// Telemetry request parser tests.
    /// </summary>
    public class TelemetryRequestParserTests
    {
        /// <summary>
        /// Verify that a request with no recognized selectors includes every section.
        /// </summary>
        [Fact]
        public void ShouldIncludeAllSectionsByDefault()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry");

            Assert.True(options.IncludeSystem);
            Assert.True(options.IncludeCpu);
            Assert.True(options.IncludeMemory);
            Assert.True(options.IncludeNetwork);
            Assert.True(options.IncludeDisk);
            Assert.True(options.IncludeGpu);
            Assert.True(options.IncludeOllama);
        }

        /// <summary>
        /// Verify presence means true and explicit false disables a selected section.
        /// </summary>
        [Fact]
        public void ShouldParseSelectiveTelemetryQuery()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?cpu&memory&network&gpu=false");

            Assert.False(options.IncludeSystem);
            Assert.True(options.IncludeCpu);
            Assert.True(options.IncludeMemory);
            Assert.True(options.IncludeNetwork);
            Assert.False(options.IncludeDisk);
            Assert.False(options.IncludeGpu);
            Assert.False(options.IncludeOllama);
        }

        /// <summary>
        /// Verify that cancellation followed by repeated stop calls exits cleanly.
        /// </summary>
        [Fact]
        public async Task ShouldStopCleanlyAfterCancellation()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "RigMonitor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            try
            {
                int port = GetFreePort();
                int stubPort = GetFreePort();
                string settingsFile = Path.Combine(tempDirectory, "rigmonitor.json");
                string settingsJson =
@"{
  ""createdUtc"": ""2026-05-18T00:00:00Z"",
  ""webserver"": {
    ""hostname"": ""127.0.0.1"",
    ""port"": " + port + @",
    ""ssl"": false,
    ""cors"": {
      ""enabled"": true,
      ""allowedOrigins"": [ ""*"" ],
      ""allowedMethods"": [ ""GET"", ""POST"", ""PUT"", ""DELETE"", ""OPTIONS"", ""HEAD"" ],
      ""allowedHeaders"": [ ""Content-Type"", ""Authorization"", ""X-Api-Key"" ],
      ""maxAgeSeconds"": 86400
    }
  },
  ""telemetry"": {
    ""dcgmExporterUrl"": ""http://127.0.0.1:" + stubPort + @"/metrics"",
    ""ollamaBaseUrl"": ""http://127.0.0.1:" + stubPort + @""",
    ""requestTimeoutMs"": 1000,
    ""warmupDelayMs"": 0
  },
  ""dashboard"": {
    ""enabled"": false,
    ""title"": ""RigMonitor Dashboard"",
    ""autoRefreshIntervalMs"": 5000
  },
  ""logging"": {
    ""servers"": [],
    ""logDirectory"": ""data/logs"",
    ""logFilename"": ""rigmonitor.log"",
    ""fileLogging"": false,
    ""includeDateInFilename"": true,
    ""consoleLogging"": false,
    ""enableColors"": false,
    ""minimumSeverity"": ""Info""
  }
}";

                await File.WriteAllTextAsync(settingsFile, settingsJson);

                using CancellationTokenSource stubCancellationTokenSource = new CancellationTokenSource();
                Task stubServerTask = RunStubHttpServerAsync(stubPort, stubCancellationTokenSource.Token);

                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
                {
                    try
                    {
                        RigMonitorServer server = await RigMonitorServer.CreateAsync(settingsFile, cancellationTokenSource.Token);
                        await server.StartAsync(cancellationTokenSource.Token);

                        cancellationTokenSource.Cancel();
                        await Task.Delay(250);

                        Exception? exception = Record.Exception(() =>
                        {
                            server.Stop();
                            server.Stop();
                        });

                        Assert.Null(exception);
                    }
                    finally
                    {
                        stubCancellationTokenSource.Cancel();
                        await stubServerTask;
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        private static async Task RunStubHttpServerAsync(int port, CancellationToken cancellationToken)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await listener.AcceptTcpClientAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[4096];
                        while (true)
                        {
                            int read = await stream.ReadAsync(buffer, cancellationToken);
                            if (read <= 0)
                            {
                                break;
                            }

                            string requestText = Encoding.ASCII.GetString(buffer, 0, read);
                            if (requestText.Contains("\r\n\r\n", StringComparison.Ordinal))
                            {
                                break;
                            }
                        }

                        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(response, cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
