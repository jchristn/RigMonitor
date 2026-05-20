namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Server;
    using RigMonitor.Server.Serialization;
    using Test.Shared.Fixtures;

    /// <summary>
    /// Verifies telemetry debug logging.
    /// </summary>
    public class TelemetryLoggingTests
    {
        /// <summary>
        /// Verify startup, shutdown, and telemetry summary debug messages are emitted.
        /// </summary>
        [Fact]
        public async Task ShouldEmitLifecycleAndTelemetryDebugLogs()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "RigMonitor.Tests", Guid.NewGuid().ToString("N"));
            string logDirectory = Path.Combine(tempDirectory, "logs");
            string logFile = Path.Combine(logDirectory, "rigmonitor.log");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                int port = GetFreePort();
                int stubPort = GetFreePort();
                string settingsFile = Path.Combine(tempDirectory, "rigmonitor.json");
                string requestPath = "/v1/telemetry?cpu&memory";

                await File.WriteAllTextAsync(settingsFile, BuildSettingsJson(port, stubPort, logDirectory));

                using CancellationTokenSource stubCancellationTokenSource = new CancellationTokenSource();
                Task stubServerTask = RunStubHttpServerAsync(stubPort, stubCancellationTokenSource.Token);

                try
                {
                    using HttpClient httpClient = new HttpClient();
                    using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

                    RigMonitorServer server = await RigMonitorServer.CreateAsync(settingsFile, cancellationTokenSource.Token);

                    try
                    {
                        await server.StartAsync(cancellationTokenSource.Token);

                        using HttpResponseMessage response = await httpClient.GetAsync("http://127.0.0.1:" + port + requestPath, cancellationTokenSource.Token);
                        response.EnsureSuccessStatusCode();
                        await response.Content.ReadAsStringAsync();
                    }
                    finally
                    {
                        server.Stop();
                    }
                }
                finally
                {
                    stubCancellationTokenSource.Cancel();
                    await stubServerTask;
                }

                string logContents = await WaitForLogContentsAsync(
                    logFile,
                    "Shutdown sequence complete.",
                    CancellationToken.None);

                Assert.Contains("Startup initialization began using settings file " + Path.GetFullPath(settingsFile), logContents);
                Assert.Contains("Startup initialization complete.", logContents);
                Assert.True(
                    Regex.IsMatch(logContents, @"GET /v1/telemetry\?cpu&memory 200 \(\d+(\.\d{2})?ms\)"),
                    "Expected telemetry summary debug log with elapsed milliseconds.");
                Assert.Contains("Shutdown sequence starting.", logContents);
                Assert.Contains("Shutdown sequence complete.", logContents);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    try
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private static string BuildSettingsJson(int port, int stubPort, string logDirectory)
        {
            RigMonitor.Core.Settings.RigMonitorSettings settings = SettingsFixture.Create();
            settings.Webserver.Hostname = "127.0.0.1";
            settings.Webserver.Port = port;
            settings.Webserver.Ssl = false;
            settings.Telemetry.DcgmExporterUrl = "http://127.0.0.1:" + stubPort + "/metrics";
            settings.Telemetry.OllamaBaseUrl = "http://127.0.0.1:" + stubPort;
            settings.Telemetry.RequestTimeoutMs = 1000;
            settings.Telemetry.WarmupDelayMs = 0;
            settings.Dashboard.Enabled = false;
            settings.Logging.LogDirectory = logDirectory;
            settings.Logging.LogFilename = "rigmonitor.log";
            settings.Logging.FileLogging = true;
            settings.Logging.IncludeDateInFilename = false;
            settings.Logging.ConsoleLogging = false;
            settings.Logging.EnableColors = false;
            settings.Logging.MinimumSeverity = LogSeverityEnum.Debug;
            return RigMonitorJsonSerializer.Serialize(settings, true);
        }

        private static async Task<string> WaitForLogContentsAsync(string logFile, string expectedText, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(logFile))
                    {
                        string logContents = await File.ReadAllTextAsync(logFile, cancellationToken).ConfigureAwait(false);
                        if (logContents.Contains(expectedText, StringComparison.Ordinal))
                        {
                            return logContents;
                        }
                    }
                }
                catch (IOException)
                {
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            return File.Exists(logFile)
                ? await File.ReadAllTextAsync(logFile, cancellationToken).ConfigureAwait(false)
                : String.Empty;
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
                        client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
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
                            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
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
                        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
