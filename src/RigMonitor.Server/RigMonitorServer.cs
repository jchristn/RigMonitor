namespace RigMonitor.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Dashboard;
    using RigMonitor.Server.Routes;
    using RigMonitor.Server.Services;
    using RigMonitor.Telemetry.Platform.Shared;
    using RigMonitor.Telemetry.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;
    using WatsonWebserver.Core.OpenApi;
    using CoreWebserverSettings = WatsonWebserver.Core.WebserverSettings;

    /// <summary>
    /// Server host for RigMonitor.
    /// </summary>
    public class RigMonitorServer
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        public RigMonitorSettings Settings { get; }

        /// <summary>
        /// Watson server instance.
        /// </summary>
        public Webserver Server { get; }

        private readonly AppLogger _Logger;
        private readonly ITelemetryService _TelemetryService;
        private readonly IRuntimeCapabilitiesService _RuntimeCapabilitiesService;
        private readonly StaticFileHandler _StaticFileHandler;
        private int _Started = 0;

        private RigMonitorServer(
            RigMonitorSettings settings,
            AppLogger logger,
            ITelemetryService telemetryService,
            IRuntimeCapabilitiesService runtimeCapabilitiesService,
            StaticFileHandler staticFileHandler)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _RuntimeCapabilitiesService = runtimeCapabilitiesService ?? throw new ArgumentNullException(nameof(runtimeCapabilitiesService));
            _StaticFileHandler = staticFileHandler ?? throw new ArgumentNullException(nameof(staticFileHandler));

            CoreWebserverSettings webserverSettings = new CoreWebserverSettings(Settings.Webserver.Hostname, Settings.Webserver.Port, Settings.Webserver.Ssl);
            Server = new Webserver(webserverSettings, DefaultRouteAsync);
        }

        /// <summary>
        /// Create a new server host from the provided settings file.
        /// </summary>
        /// <param name="settingsFile">Settings file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Server host.</returns>
        public static async Task<RigMonitorServer> CreateAsync(string settingsFile, CancellationToken cancellationToken)
        {
            RigMonitorSettings settings = await SettingsManager.LoadAsync(settingsFile, cancellationToken).ConfigureAwait(false);
            AppLogger logger = new AppLogger(settings.Logging);
            logger.Debug("Startup initialization began using settings file " + Path.GetFullPath(settingsFile));

            IDcgmExporterClient dcgmClient = new DcgmExporterClient(settings.Telemetry);
            IOllamaClient ollamaClient = new OllamaClient(settings.Telemetry);
            IRuntimeCapabilitiesService runtimeCapabilitiesService = new RuntimeCapabilitiesService(
                settings.Telemetry,
                settings.Dashboard.Enabled,
                dcgmClient,
                ollamaClient);

            await runtimeCapabilitiesService.InitializeAsync(cancellationToken).ConfigureAwait(false);

            ISystemTelemetryProvider systemTelemetryProvider = SystemTelemetryProviderFactory.Create();
            IMemoryInfoProvider memoryInfoProvider = new DefaultMemoryInfoProvider();
            INetworkRateSampler networkRateSampler = new DefaultNetworkRateSampler();
            IGpuTelemetryProvider gpuTelemetryProvider = new NvidiaDcgmGpuTelemetryProvider(settings.Telemetry, dcgmClient);
            ITelemetryService telemetryService = new TelemetryService(
                settings.Telemetry,
                systemTelemetryProvider,
                memoryInfoProvider,
                networkRateSampler,
                runtimeCapabilitiesService,
                gpuTelemetryProvider,
                ollamaClient);

            await telemetryService.WarmupAsync(cancellationToken).ConfigureAwait(false);

            StaticFileHandler staticFileHandler = new StaticFileHandler(settings, logger);
            RigMonitorServer host = new RigMonitorServer(settings, logger, telemetryService, runtimeCapabilitiesService, staticFileHandler);

            logger.Debug("Startup initialization complete.");
            logger.Info("Settings loaded from " + settingsFile);
            logger.Info("Runtime platform: " + runtimeCapabilitiesService.Current.HostPlatform);
            logger.Info("DCGM available: " + runtimeCapabilitiesService.Current.NvidiaAvailable);
            logger.Info("Ollama available: " + runtimeCapabilitiesService.Current.OllamaAvailable);
            return host;
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.CompareExchange(ref _Started, 1, 0) != 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                ConfigureServer();
                ConfigureRoutes();
                // Program owns the shutdown sequence and calls Stop() exactly once.
                Server.Start(CancellationToken.None);
                _Logger.Info("RigMonitor listening on http" + (Settings.Webserver.Ssl ? "s" : String.Empty) + "://" + Settings.Webserver.Hostname + ":" + Settings.Webserver.Port);
                return Task.CompletedTask;
            }
            catch
            {
                Interlocked.Exchange(ref _Started, 0);
                throw;
            }
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop()
        {
            if (Interlocked.Exchange(ref _Started, 0) == 0)
            {
                return;
            }

            _Logger.Debug("Shutdown sequence starting.");

            if (Server.IsListening)
            {
                Server.Stop();
            }

            _Logger.Info("RigMonitor stopped");
            _Logger.Debug("Shutdown sequence complete.");
        }

        private void ConfigureServer()
        {
            Server.UseHealthCheck((HealthCheckSettings health) =>
            {
                health.Path = "/livez";
                health.RequireAuthentication = false;
            });

            Server.UseOpenApi((OpenApiSettings openApi) =>
            {
                openApi.Info.Title = "RigMonitor API";
                openApi.Info.Version = "1.0.0";
                openApi.Info.Description = "Rig and workstation telemetry API.";
                openApi.DocumentPath = "/openapi.json";
                openApi.SwaggerUiPath = "/openapi";
            });

            Server.Routes.Preflight = PreflightRouteAsync;
            Server.Routes.PostRouting = PostRoutingRouteAsync;
        }

        private void ConfigureRoutes()
        {
            new GeneralRoutes(_TelemetryService, _RuntimeCapabilitiesService).Register(Server);
            new TelemetryRoutes(_TelemetryService, _Logger).Register(Server);

            if (Settings.Dashboard.Enabled)
            {
                new DashboardRoutes(_StaticFileHandler).Register(Server);
            }
        }

        private async Task PreflightRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            ApplyCorsHeaders(context);
            context.Response.Headers.Add("Access-Control-Max-Age", Settings.Webserver.Cors.MaxAgeSeconds.ToString());
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private Task PostRoutingRouteAsync(HttpContextBase context)
        {
            context.Timestamp.End = DateTime.UtcNow;
            ApplyCorsHeaders(context);

            if (String.Equals(context.Request.Url.RawWithoutQuery, "/v1/telemetry", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            string duration = context.Timestamp.TotalMs.HasValue
                ? context.Timestamp.TotalMs.Value.ToString("F2")
                : "?";

            _Logger.Debug(context.Request.Method + " " + context.Request.Url.RawWithQuery + " " + context.Response.StatusCode + " (" + duration + "ms)");
            return Task.CompletedTask;
        }

        private void ApplyCorsHeaders(HttpContextBase context)
        {
            if (!Settings.Webserver.Cors.Enabled)
            {
                return;
            }

            string allowOrigin = ResolveAllowedOrigin(context);
            context.Response.Headers.Add("Access-Control-Allow-Origin", allowOrigin);
            context.Response.Headers.Add("Access-Control-Allow-Methods", String.Join(", ", Settings.Webserver.Cors.AllowedMethods));
            context.Response.Headers.Add("Access-Control-Allow-Headers", String.Join(", ", Settings.Webserver.Cors.AllowedHeaders));
        }

        private string ResolveAllowedOrigin(HttpContextBase context)
        {
            if (Settings.Webserver.Cors.AllowedOrigins.Any(node => node == "*"))
            {
                return "*";
            }

            string requestOrigin = context.Request.RetrieveHeaderValue("Origin");
            if (!String.IsNullOrWhiteSpace(requestOrigin)
                && Settings.Webserver.Cors.AllowedOrigins.Any(node => String.Equals(node, requestOrigin, StringComparison.OrdinalIgnoreCase)))
            {
                return requestOrigin;
            }

            return Settings.Webserver.Cors.AllowedOrigins.FirstOrDefault() ?? "*";
        }

        private static async Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            await context.Response.Send("Not Found", context.Token).ConfigureAwait(false);
        }
    }
}
