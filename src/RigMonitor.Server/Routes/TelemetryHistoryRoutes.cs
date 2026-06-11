namespace RigMonitor.Server.Routes
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using RigMonitor.Core.Database;
    using RigMonitor.Core.Models;
    using RigMonitor.Server.Serialization;
    using RigMonitor.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Telemetry history route registrar.
    /// </summary>
    public class TelemetryHistoryRoutes
    {
        private readonly DatabaseDriverBase _Database;
        private readonly TelemetryPersistenceService _PersistenceService;

        /// <summary>
        /// Instantiate the registrar.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="persistenceService">Telemetry persistence service.</param>
        public TelemetryHistoryRoutes(DatabaseDriverBase database, TelemetryPersistenceService persistenceService)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _PersistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        }

        /// <summary>
        /// Register telemetry history routes.
        /// </summary>
        /// <param name="server">Watson server.</param>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.POST,
                "/v1/telemetry/history/enumerate",
                EnumerateRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Enumerate telemetry history", "Telemetry History")
                    .WithDescription("Returns telemetry history records using the EnumerationQuery continuation pattern."));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.POST,
                "/v1/telemetry/history/search",
                SearchRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Search telemetry history", "Telemetry History")
                    .WithDescription("Returns a request-history style paged search result for persisted telemetry samples."));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.POST,
                "/v1/telemetry/history/rollups",
                RollupsRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Roll up telemetry history", "Telemetry History")
                    .WithDescription("Returns bucketized average, minimum, and maximum telemetry over a requested time range."));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1/telemetry/history/status",
                StatusRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Telemetry history status", "Telemetry History")
                    .WithDescription("Returns persistence worker and database status."));

            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.DELETE,
                "/v1/telemetry/history",
                DeleteBulkRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Bulk delete telemetry history", "Telemetry History")
                    .WithDescription("Deletes telemetry history samples matching the supplied search filter."));

            server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/v1/telemetry/history/{sampleId}",
                DetailRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Get telemetry history sample", "Telemetry History")
                    .WithDescription("Returns a persisted telemetry sample and its original snapshot payload."));

            server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.DELETE,
                "/v1/telemetry/history/{sampleId}",
                DeleteRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Delete telemetry history sample", "Telemetry History")
                    .WithDescription("Deletes one persisted telemetry history sample."));
        }

        private async Task EnumerateRouteAsync(HttpContextBase context)
        {
            try
            {
                EnumerationQuery query = ReadBody<EnumerationQuery>(context);
                ValidateDateRange(query.StartUtc, query.EndUtc);

                EnumerationResult<TelemetrySampleRecord> result = await _Database.TelemetryHistory
                    .EnumerateAsync(query, context.Token)
                    .ConfigureAwait(false);

                await HttpResponder.WriteJsonAsync(context, result, 200).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsBadRequest(exception))
            {
                await WriteErrorAsync(context, 400, exception.Message).ConfigureAwait(false);
            }
        }

        private async Task SearchRouteAsync(HttpContextBase context)
        {
            try
            {
                TelemetryHistorySearchFilter filter = ReadBody<TelemetryHistorySearchFilter>(context);
                ValidateDateRange(filter.StartUtc, filter.EndUtc);

                TelemetryHistorySearchResult result = await _Database.TelemetryHistory
                    .SearchAsync(filter, context.Token)
                    .ConfigureAwait(false);

                await HttpResponder.WriteJsonAsync(context, result, 200).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsBadRequest(exception))
            {
                await WriteErrorAsync(context, 400, exception.Message).ConfigureAwait(false);
            }
        }

        private async Task DetailRouteAsync(HttpContextBase context)
        {
            string? sampleId = context.Request.Url.Parameters["sampleId"];
            if (String.IsNullOrWhiteSpace(sampleId))
            {
                await WriteErrorAsync(context, 400, "Sample ID is required.").ConfigureAwait(false);
                return;
            }

            TelemetrySampleDetail? detail = await _Database.TelemetryHistory
                .ReadDetailAsync(sampleId, context.Token)
                .ConfigureAwait(false);

            if (detail == null)
            {
                await WriteErrorAsync(context, 404, "Telemetry sample '" + sampleId + "' was not found.").ConfigureAwait(false);
                return;
            }

            await HttpResponder.WriteJsonAsync(context, detail, 200).ConfigureAwait(false);
        }

        private async Task RollupsRouteAsync(HttpContextBase context)
        {
            try
            {
                TelemetryRollupRequest request = ReadBody<TelemetryRollupRequest>(context);
                if (request.EndUtc <= request.StartUtc)
                {
                    throw new ArgumentOutOfRangeException(nameof(request.EndUtc), "EndUtc must be later than StartUtc.");
                }

                TelemetryRollupResult result = await _Database.TelemetryHistory
                    .RollupAsync(request, context.Token)
                    .ConfigureAwait(false);

                await HttpResponder.WriteJsonAsync(context, result, 200).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsBadRequest(exception))
            {
                await WriteErrorAsync(context, 400, exception.Message).ConfigureAwait(false);
            }
        }

        private async Task StatusRouteAsync(HttpContextBase context)
        {
            await HttpResponder.WriteJsonAsync(context, _PersistenceService.GetStatus(), 200).ConfigureAwait(false);
        }

        private async Task DeleteRouteAsync(HttpContextBase context)
        {
            string? sampleId = context.Request.Url.Parameters["sampleId"];
            if (String.IsNullOrWhiteSpace(sampleId))
            {
                await WriteErrorAsync(context, 400, "Sample ID is required.").ConfigureAwait(false);
                return;
            }

            bool deleted = await _Database.TelemetryHistory.DeleteAsync(sampleId, context.Token).ConfigureAwait(false);
            if (!deleted)
            {
                await WriteErrorAsync(context, 404, "Telemetry sample '" + sampleId + "' was not found.").ConfigureAwait(false);
                return;
            }

            await HttpResponder.WriteJsonAsync(
                context,
                new DeleteResult
                {
                    Deleted = true,
                    DeletedCount = 1L
                },
                200).ConfigureAwait(false);
        }

        private async Task DeleteBulkRouteAsync(HttpContextBase context)
        {
            try
            {
                TelemetryHistorySearchFilter filter = ReadBody<TelemetryHistorySearchFilter>(context);
                ValidateDateRange(filter.StartUtc, filter.EndUtc);

                long deletedCount = await _Database.TelemetryHistory
                    .DeleteBulkAsync(filter, context.Token)
                    .ConfigureAwait(false);

                await HttpResponder.WriteJsonAsync(
                    context,
                    new DeleteResult
                    {
                        Deleted = deletedCount > 0L,
                        DeletedCount = deletedCount
                    },
                    200).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsBadRequest(exception))
            {
                await WriteErrorAsync(context, 400, exception.Message).ConfigureAwait(false);
            }
        }

        private static T ReadBody<T>(HttpContextBase context) where T : new()
        {
            string body = context.Request.DataAsString;
            if (String.IsNullOrWhiteSpace(body))
            {
                return new T();
            }

            T? parsed = RigMonitorJsonSerializer.Deserialize<T>(body);
            if (parsed == null)
            {
                throw new InvalidDataException("Request body could not be deserialized as " + typeof(T).Name + ".");
            }

            return parsed;
        }

        private static void ValidateDateRange(DateTime? startUtc, DateTime? endUtc)
        {
            if (startUtc.HasValue && endUtc.HasValue && endUtc.Value < startUtc.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(endUtc), "EndUtc must be greater than or equal to StartUtc.");
            }
        }

        private static bool IsBadRequest(Exception exception)
        {
            return exception is JsonException
                || exception is InvalidDataException
                || exception is ArgumentException;
        }

        private static Task WriteErrorAsync(HttpContextBase context, int statusCode, string message)
        {
            return HttpResponder.WriteJsonAsync(
                context,
                new
                {
                    Error = message
                },
                statusCode);
        }
    }
}
