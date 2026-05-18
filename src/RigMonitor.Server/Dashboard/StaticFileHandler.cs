namespace RigMonitor.Server.Dashboard
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using RigMonitor.Core.Settings;
    using RigMonitor.Server.Services;
    using WatsonWebserver.Core;

    /// <summary>
    /// Serves dashboard static files with SPA fallback support.
    /// </summary>
    public class StaticFileHandler
    {
        private readonly AppLogger _Logger;
        private readonly List<string> _SearchRoots = new List<string>();

        /// <summary>
        /// Instantiate the handler.
        /// </summary>
        /// <param name="settings">Application settings.</param>
        /// <param name="logger">Application logger.</param>
        public StaticFileHandler(RigMonitorSettings settings, AppLogger logger)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _Logger = logger;
            _SearchRoots.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dashboard", "dist")));
            _SearchRoots.Add(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        }

        /// <summary>
        /// Serve the dashboard application.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        public async Task HandleDashboardAsync(HttpContextBase context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            string rawPath = context.Request.Url.RawWithoutQuery ?? "/dashboard";
            string relativePath = rawPath.StartsWith("/dashboard", StringComparison.OrdinalIgnoreCase)
                ? rawPath.Substring("/dashboard".Length).TrimStart('/')
                : rawPath.TrimStart('/');

            if (String.IsNullOrWhiteSpace(relativePath))
            {
                relativePath = "index.html";
            }

            string? filePath = ResolveFile(relativePath);
            if (filePath == null && !HasExtension(relativePath))
            {
                filePath = ResolveFile("index.html");
            }

            if (filePath == null)
            {
                _Logger.Warn("Dashboard asset request could not be resolved: " + rawPath);
                context.Response.StatusCode = 404;
                await context.Response.Send("Not Found", context.Token).ConfigureAwait(false);
                return;
            }

            await SendFileAsync(context, filePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Serve a root static file such as the favicon.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="relativePath">Relative file path.</param>
        public async Task HandleRootFileAsync(HttpContextBase context, string relativePath)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (String.IsNullOrWhiteSpace(relativePath)) throw new ArgumentNullException(nameof(relativePath));

            string? filePath = ResolveFile(relativePath.TrimStart('/'));
            if (filePath == null)
            {
                context.Response.StatusCode = 404;
                await context.Response.Send("Not Found", context.Token).ConfigureAwait(false);
                return;
            }

            await SendFileAsync(context, filePath).ConfigureAwait(false);
        }

        private string? ResolveFile(string relativePath)
        {
            foreach (string root in _SearchRoots)
            {
                string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
                if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasExtension(string path)
        {
            return path.Contains('.', StringComparison.Ordinal);
        }

        private static async Task SendFileAsync(HttpContextBase context, string filePath)
        {
            byte[] data = await File.ReadAllBytesAsync(filePath, context.Token).ConfigureAwait(false);
            context.Response.StatusCode = 200;
            context.Response.ContentType = GetContentType(filePath);
            await context.Response.Send(data, context.Token).ConfigureAwait(false);
        }

        private static string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".css" => "text/css",
                ".html" => "text/html; charset=utf-8",
                ".ico" => "image/x-icon",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".svg" => "image/svg+xml",
                ".txt" => "text/plain; charset=utf-8",
                _ => "application/octet-stream"
            };
        }
    }
}
