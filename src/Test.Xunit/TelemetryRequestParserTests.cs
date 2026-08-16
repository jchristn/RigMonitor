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
            Assert.True(options.IncludeVllm);
            Assert.True(options.IncludeUtilyze);
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
            Assert.False(options.IncludeVllm);
            Assert.False(options.IncludeUtilyze);
        }

        /// <summary>
        /// Verify Utilyze can be selected independently.
        /// </summary>
        [Fact]
        public void ShouldParseUtilyzeTelemetryQuery()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?utilyze");

            Assert.False(options.IncludeSystem);
            Assert.False(options.IncludeCpu);
            Assert.False(options.IncludeMemory);
            Assert.False(options.IncludeNetwork);
            Assert.False(options.IncludeDisk);
            Assert.False(options.IncludeGpu);
            Assert.False(options.IncludeOllama);
            Assert.False(options.IncludeVllm);
            Assert.True(options.IncludeUtilyze);
        }

        /// <summary>
        /// Verify vLLM can be selected independently.
        /// </summary>
        [Fact]
        public void ShouldParseVllmTelemetryQuery()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?vllm");

            Assert.False(options.IncludeSystem);
            Assert.False(options.IncludeCpu);
            Assert.False(options.IncludeMemory);
            Assert.False(options.IncludeNetwork);
            Assert.False(options.IncludeDisk);
            Assert.False(options.IncludeGpu);
            Assert.False(options.IncludeOllama);
            Assert.True(options.IncludeVllm);
            Assert.False(options.IncludeUtilyze);
        }

        /// <summary>
        /// Verify null, empty, and whitespace inputs fall back to including every section.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ShouldIncludeAllSectionsForMissingInput(string? rawWithQuery)
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse(rawWithQuery);

            Assert.True(options.IncludeSystem);
            Assert.True(options.IncludeCpu);
            Assert.True(options.IncludeMemory);
            Assert.True(options.IncludeNetwork);
            Assert.True(options.IncludeDisk);
            Assert.True(options.IncludeGpu);
            Assert.True(options.IncludeOllama);
            Assert.True(options.IncludeVllm);
            Assert.True(options.IncludeUtilyze);
        }

        /// <summary>
        /// Verify a path with an empty or trailing query string includes every section.
        /// </summary>
        [Theory]
        [InlineData("/v1/telemetry?")]
        [InlineData("/v1/telemetry?&&")]
        public void ShouldIncludeAllSectionsForEmptyQuery(string rawWithQuery)
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse(rawWithQuery);

            Assert.True(options.IncludeSystem);
            Assert.True(options.IncludeGpu);
            Assert.True(options.IncludeUtilyze);
        }

        /// <summary>
        /// Verify a query containing only unrecognized selectors falls back to including every section.
        /// </summary>
        [Fact]
        public void ShouldIncludeAllSectionsWhenNoRecognizedSelectorsPresent()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?foo&bar=false&baz=true");

            Assert.True(options.IncludeSystem);
            Assert.True(options.IncludeCpu);
            Assert.True(options.IncludeGpu);
            Assert.True(options.IncludeUtilyze);
        }

        /// <summary>
        /// Verify unrecognized selectors are ignored while recognized ones still take effect.
        /// </summary>
        [Fact]
        public void ShouldIgnoreUnrecognizedSelectorsAlongsideRecognizedOnes()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?cpu&unknown&disk=false");

            Assert.False(options.IncludeSystem);
            Assert.True(options.IncludeCpu);
            Assert.False(options.IncludeDisk);
            Assert.False(options.IncludeGpu);
        }

        /// <summary>
        /// Verify a recognized selector with an empty value is treated as enabled.
        /// </summary>
        [Fact]
        public void ShouldTreatEmptySelectorValueAsEnabled()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?gpu=");

            Assert.True(options.IncludeGpu);
            Assert.False(options.IncludeCpu);
        }

        /// <summary>
        /// Verify selector keys and the false value are matched case-insensitively.
        /// </summary>
        [Fact]
        public void ShouldMatchSelectorsCaseInsensitively()
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse("/v1/telemetry?CPU&GPU=FALSE");

            Assert.True(options.IncludeCpu);
            Assert.False(options.IncludeGpu);
            Assert.False(options.IncludeMemory);
        }

        /// <summary>
        /// Verify only the literal token "false" disables a section; other values enable it.
        /// </summary>
        [Theory]
        [InlineData("/v1/telemetry?gpu=true", true)]
        [InlineData("/v1/telemetry?gpu=0", true)]
        [InlineData("/v1/telemetry?gpu=no", true)]
        [InlineData("/v1/telemetry?gpu=false", false)]
        public void ShouldOnlyDisableSectionForFalseToken(string rawWithQuery, bool expectedIncludeGpu)
        {
            TelemetryRequestOptions options = TelemetryRequestParser.Parse(rawWithQuery);

            Assert.Equal(expectedIncludeGpu, options.IncludeGpu);
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
