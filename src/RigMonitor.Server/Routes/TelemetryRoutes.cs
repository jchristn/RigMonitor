namespace RigMonitor.Server.Routes
{
    using System;
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

        /// <summary>
        /// Instantiate the registrar.
        /// </summary>
        /// <param name="telemetryService">Telemetry service.</param>
        public TelemetryRoutes(ITelemetryService telemetryService)
        {
            _TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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
            TelemetryRequestOptions requestOptions = TelemetryRequestParser.Parse(context.Request.Url.RawWithQuery);

            await HttpResponder.WriteJsonAsync(
                context,
                await _TelemetryService.GetSnapshotAsync(requestOptions, context.Token).ConfigureAwait(false),
                200).ConfigureAwait(false);
        }
    }
}
