namespace RigMonitor.Server.Routes
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using RigMonitor.Server.Dashboard;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Dashboard route registrar.
    /// </summary>
    public class DashboardRoutes
    {
        private readonly StaticFileHandler _StaticFileHandler;

        /// <summary>
        /// Instantiate the registrar.
        /// </summary>
        /// <param name="staticFileHandler">Static file handler.</param>
        public DashboardRoutes(StaticFileHandler staticFileHandler)
        {
            _StaticFileHandler = staticFileHandler ?? throw new ArgumentNullException(nameof(staticFileHandler));
        }

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Watson server.</param>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", RootRouteAsync);
            server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/favicon.ico", FaviconRouteAsync);
            server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/dashboard(?:/.*)?$", RegexOptions.IgnoreCase), DashboardRouteAsync);
        }

        private static async Task RootRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 302;
            context.Response.Headers.Add("Location", "/dashboard");
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private Task FaviconRouteAsync(HttpContextBase context)
        {
            return _StaticFileHandler.HandleRootFileAsync(context, "favicon.ico");
        }

        private Task DashboardRouteAsync(HttpContextBase context)
        {
            return _StaticFileHandler.HandleDashboardAsync(context);
        }
    }
}
