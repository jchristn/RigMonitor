namespace RigMonitor.Server.Routes
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;
    using RigMonitor.Core.Models;
    using RigMonitor.Core.Services.Interfaces;
    using RigMonitor.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Telemetry route registrar.
    /// </summary>
    public class TelemetryRoutes
    {
        private readonly ITelemetryService _TelemetryService;
        private readonly AppLogger _Logger;

        /// <summary>
        /// Instantiate the registrar.
        /// </summary>
        /// <param name="telemetryService">Telemetry service.</param>
        /// <param name="logger">Application logger.</param>
        public TelemetryRoutes(ITelemetryService telemetryService, AppLogger logger)
        {
            _TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Watson server.</param>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1/telemetry",
                TelemetryRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Telemetry", "Telemetry").WithDescription("Collect a RigMonitor telemetry snapshot. Optional query keys: system, cpu, memory, network, disk, gpu, ollama. Presence means true unless explicitly set to false. If no recognized keys are supplied, all sections are included."));
        }

        private async Task TelemetryRouteAsync(HttpContextBase context)
        {
            string rawWithQuery = context.Request.Url.RawWithQuery ?? "/v1/telemetry";
            _Logger.Debug("Telemetry request received: " + context.Request.Method + " " + rawWithQuery);

            Stopwatch stopwatch = Stopwatch.StartNew();
            TelemetryRequestOptions requestOptions = TelemetryRequestParser.Parse(rawWithQuery);

            await HttpResponder.WriteJsonAsync(
                context,
                await _TelemetryService.GetSnapshotAsync(requestOptions, context.Token).ConfigureAwait(false),
                200).ConfigureAwait(false);

            stopwatch.Stop();
            _Logger.Debug(
                "Telemetry response sent: "
                + context.Response.StatusCode
                + " "
                + context.Request.Method
                + " "
                + rawWithQuery
                + " ("
                + stopwatch.Elapsed.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture)
                + "ms)");
        }
    }
}
