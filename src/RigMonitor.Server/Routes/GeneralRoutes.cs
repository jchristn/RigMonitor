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
    /// General API route registrar.
    /// </summary>
    public class GeneralRoutes
    {
        private readonly ITelemetryService _TelemetryService;
        private readonly IRuntimeCapabilitiesService _RuntimeCapabilitiesService;

        /// <summary>
        /// Instantiate the registrar.
        /// </summary>
        /// <param name="telemetryService">Telemetry service.</param>
        /// <param name="runtimeCapabilitiesService">Capabilities service.</param>
        public GeneralRoutes(ITelemetryService telemetryService, IRuntimeCapabilitiesService runtimeCapabilitiesService)
        {
            _TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _RuntimeCapabilitiesService = runtimeCapabilitiesService ?? throw new ArgumentNullException(nameof(runtimeCapabilitiesService));
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
                "/readyz",
                ReadyzRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Readiness", "Health").WithDescription("Readiness including telemetry warmup state."));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1/capabilities",
                CapabilitiesRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Capabilities", "Telemetry").WithDescription("Runtime capabilities including DCGM and Ollama availability."));
        }

        private Task ReadyzRouteAsync(HttpContextBase context)
        {
            ServiceStatus response = new ServiceStatus
            {
                Status = _TelemetryService.IsWarm ? "ready" : "warming",
                Ready = _TelemetryService.IsWarm,
                Message = _TelemetryService.IsWarm ? "Telemetry samplers are warm." : "Telemetry warmup has not completed.",
                TimestampUtc = DateTime.UtcNow
            };

            return HttpResponder.WriteJsonAsync(context, response, _TelemetryService.IsWarm ? 200 : 503);
        }

        private Task CapabilitiesRouteAsync(HttpContextBase context)
        {
            return HttpResponder.WriteJsonAsync(context, _RuntimeCapabilitiesService.Current, 200);
        }
    }
}
