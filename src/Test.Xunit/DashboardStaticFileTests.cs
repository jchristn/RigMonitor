namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Dashboard;
    using RigMonitor.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Dashboard static file handler tests.
    /// </summary>
    public class DashboardStaticFileTests
    {
        private const string _DashboardRootEnvironmentVariable = "RIGMONITOR_DASHBOARD_ROOT";

        /// <summary>
        /// Verify dashboard files, SPA fallback, and negative static asset requests.
        /// </summary>
        [Fact]
        public async Task ShouldServeDashboardFilesAndRejectMissingOrUnsafeAssets()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "RigMonitor.Tests", Guid.NewGuid().ToString("N"));
            string dashboardRoot = Path.Combine(tempDirectory, "dashboard-root");
            string assetsDirectory = Path.Combine(dashboardRoot, "assets");
            string secretFile = Path.Combine(tempDirectory, "secret.txt");
            Directory.CreateDirectory(assetsDirectory);

            string? previousDashboardRoot = Environment.GetEnvironmentVariable(_DashboardRootEnvironmentVariable);

            try
            {
                await File.WriteAllTextAsync(Path.Combine(dashboardRoot, "index.html"), "<!doctype html><main>RigMonitor shell</main>");
                await File.WriteAllTextAsync(Path.Combine(assetsDirectory, "app.js"), "console.log('dashboard asset');");
                await File.WriteAllTextAsync(secretFile, "outside dashboard root");

                Environment.SetEnvironmentVariable(_DashboardRootEnvironmentVariable, dashboardRoot);

                StaticFileHandler handler = new StaticFileHandler(CreateSettings(tempDirectory), CreateLogger(tempDirectory));

                using HttpContext shellContext = CreateContext("/dashboard");
                await handler.HandleDashboardAsync(shellContext);

                Assert.Equal(200, shellContext.Response.StatusCode);
                Assert.Equal("text/html; charset=utf-8", shellContext.Response.ContentType);
                Assert.Contains("RigMonitor shell", shellContext.Response.DataAsString);

                using HttpContext fallbackContext = CreateContext("/dashboard/deep/link");
                await handler.HandleDashboardAsync(fallbackContext);

                Assert.Equal(200, fallbackContext.Response.StatusCode);
                Assert.Contains("RigMonitor shell", fallbackContext.Response.DataAsString);

                using HttpContext assetContext = CreateContext("/dashboard/assets/app.js");
                await handler.HandleDashboardAsync(assetContext);

                Assert.Equal(200, assetContext.Response.StatusCode);
                Assert.Equal("application/javascript", assetContext.Response.ContentType);
                Assert.Equal("console.log('dashboard asset');", assetContext.Response.DataAsString);

                using HttpContext missingAssetContext = CreateContext("/dashboard/assets/missing.js");
                await handler.HandleDashboardAsync(missingAssetContext);

                Assert.Equal(404, missingAssetContext.Response.StatusCode);
                Assert.Equal("Not Found", missingAssetContext.Response.DataAsString);

                using HttpContext traversalContext = CreateContext("/dashboard/../secret.txt");
                await handler.HandleDashboardAsync(traversalContext);

                Assert.Equal(404, traversalContext.Response.StatusCode);
                Assert.Equal("Not Found", traversalContext.Response.DataAsString);
            }
            finally
            {
                Environment.SetEnvironmentVariable(_DashboardRootEnvironmentVariable, previousDashboardRoot);

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

        private static HttpContext CreateContext(string rawPath)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            return new HttpContext
            {
                Request = new HttpRequest
                {
                    Url = new UrlDetails("http://127.0.0.1" + rawPath, rawPath)
                },
                Response = new HttpResponse(),
                TokenSource = tokenSource
            };
        }

        private static RigMonitorSettings CreateSettings(string tempDirectory)
        {
            return new RigMonitorSettings
            {
                Logging = new LoggingSettings
                {
                    LogDirectory = Path.Combine(tempDirectory, "logs"),
                    LogFilename = "rigmonitor.log",
                    FileLogging = false,
                    ConsoleLogging = false,
                    EnableColors = false,
                    MinimumSeverity = LogSeverityEnum.Debug
                }
            };
        }

        private static AppLogger CreateLogger(string tempDirectory)
        {
            return new AppLogger(CreateSettings(tempDirectory).Logging);
        }
    }
}
