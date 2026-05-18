namespace RigMonitor.Server.Services
{
    using System;
    using System.Threading.Tasks;
    using RigMonitor.Server.Serialization;
    using WatsonWebserver.Core;

    /// <summary>
    /// HTTP response helper methods.
    /// </summary>
    public static class HttpResponder
    {
        /// <summary>
        /// Write a JSON response.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="value">Payload.</param>
        /// <param name="statusCode">HTTP status code.</param>
        public static async Task WriteJsonAsync(HttpContextBase context, object value, int statusCode)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (value == null) throw new ArgumentNullException(nameof(value));

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.Send(RigMonitorJsonSerializer.Serialize(value), context.Token).ConfigureAwait(false);
        }
    }
}
