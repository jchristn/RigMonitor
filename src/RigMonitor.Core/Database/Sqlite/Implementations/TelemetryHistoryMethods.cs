namespace RigMonitor.Core.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using RigMonitor.Core.Database.Interfaces;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Helpers;
    using RigMonitor.Core.Models;

    /// <summary>
    /// SQLite telemetry history implementation.
    /// </summary>
    internal class TelemetryHistoryMethods : ITelemetryHistoryMethods
    {
        private const string EntryColumns =
            "id, hostname, collectedutc, persistedutc, hostplatform, nvidiaavailable, ollamaavailable, vllmavailable, utilyzeavailable, " +
            "cpuutilizationpercent, logicalcorecount, memorytotalbytes, memoryusedbytes, memoryavailablebytes, memoryutilizationpercent, " +
            "networkreceivebytespersecond, networktransmitbytespersecond, diskreadoperationspersecond, diskwriteoperationspersecond, " +
            "diskreadqueuedepth, diskwritequeuedepth, gpudevicecount, gpuaverageutilizationpercent, gpuaveragememoryutilizationpercent, " +
            "gpuaveragetemperaturecelsius, gputotalpowerusagewatts, ollamaavailablemodelcount, ollamaloadedmodelcount, vllmrunningrequests, " +
            "vllmwaitingrequests, vllmgpucacheusagepercent, utilyzedevicecount";

        private static readonly JsonSerializerOptions _JsonOptions = BuildJsonOptions();
        private readonly SqliteDatabaseDriver _Driver;
        private readonly string _Hostname;

        internal TelemetryHistoryMethods(SqliteDatabaseDriver driver, string hostname)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Hostname = String.IsNullOrWhiteSpace(hostname) ? "localhost" : hostname.Trim();
        }

        public async Task<TelemetrySampleDetail> CreateAsync(TelemetrySnapshot snapshot, CancellationToken token = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            token.ThrowIfCancellationRequested();

            TelemetrySampleDetail detail = BuildDetail(snapshot);
            string snapshotJson = JsonSerializer.Serialize(snapshot, _JsonOptions);

            await _Driver.ExecuteInTransactionAsync(async (connection, transaction, cancellationToken) =>
            {
                using (SqliteCommand command = new SqliteCommand(BuildInsertSampleQuery(), connection, transaction))
                {
                    SqliteDatabaseDriver.AddParameters(command, BuildSampleParameters(detail, snapshotJson));
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                if (snapshot.Gpu != null)
                {
                    foreach (GpuDeviceTelemetry device in snapshot.Gpu.Devices)
                    {
                        using (SqliteCommand command = new SqliteCommand(BuildInsertGpuSampleQuery(), connection, transaction))
                        {
                            SqliteDatabaseDriver.AddParameters(command, BuildGpuParameters(detail, device));
                            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }, token).ConfigureAwait(false);

            return detail;
        }

        public async Task<TelemetrySampleRecord?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            token.ThrowIfCancellationRequested();

            DataTable result = await _Driver.ExecuteQueryAsync(
                "SELECT " + EntryColumns + " FROM telemetry_samples WHERE id = @id;",
                token,
                new SqliteParameter("@id", id)).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return Converters.TelemetrySampleRecordFromDataRow(result.Rows[0]);
        }

        public async Task<TelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            token.ThrowIfCancellationRequested();

            DataTable result = await _Driver.ExecuteQueryAsync(
                "SELECT " + EntryColumns + ", snapshotjson FROM telemetry_samples WHERE id = @id;",
                token,
                new SqliteParameter("@id", id)).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;

            TelemetrySampleRecord record = Converters.TelemetrySampleRecordFromDataRow(result.Rows[0]);
            string snapshotJson = Converters.GetString(result.Rows[0], "snapshotjson");
            TelemetrySnapshot? snapshot = JsonSerializer.Deserialize<TelemetrySnapshot>(snapshotJson, _JsonOptions);

            return CopyToDetail(record, snapshot ?? new TelemetrySnapshot());
        }

        public async Task<EnumerationResult<TelemetrySampleRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            query ??= new EnumerationQuery();
            token.ThrowIfCancellationRequested();

            Stopwatch stopwatch = Stopwatch.StartNew();
            int offset = ParseContinuationToken(query.ContinuationToken);
            List<SqliteParameter> parameters = new List<SqliteParameter>();
            string whereClause = BuildEnumerationWhere(query, parameters);

            DataTable countResult = await _Driver.ExecuteQueryAsync(
                "SELECT COUNT(*) AS cnt FROM telemetry_samples " + whereClause + ";",
                token,
                parameters.ToArray()).ConfigureAwait(false);

            long totalRecords = GetCount(countResult);
            string ordering = query.Ordering == EnumerationOrderEnum.CreatedAscending
                ? "collectedutc ASC, id ASC"
                : "collectedutc DESC, id DESC";

            parameters.Add(new SqliteParameter("@limit", query.MaxResults));
            parameters.Add(new SqliteParameter("@offset", offset));

            DataTable result = await _Driver.ExecuteQueryAsync(
                "SELECT " + EntryColumns + " FROM telemetry_samples " + whereClause + " ORDER BY " + ordering + " LIMIT @limit OFFSET @offset;",
                token,
                parameters.ToArray()).ConfigureAwait(false);

            List<TelemetrySampleRecord> records = RecordsFromTable(result);
            long recordsRemaining = Math.Max(0L, totalRecords - offset - records.Count);
            stopwatch.Stop();

            return new EnumerationResult<TelemetrySampleRecord>
            {
                Success = true,
                MaxResults = query.MaxResults,
                TotalRecords = totalRecords,
                RecordsRemaining = recordsRemaining,
                EndOfResults = recordsRemaining <= 0L,
                ContinuationToken = recordsRemaining <= 0L ? null : (offset + records.Count).ToString(),
                Objects = records,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }

        public async Task<TelemetryHistorySearchResult> SearchAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default)
        {
            filter ??= new TelemetryHistorySearchFilter();
            token.ThrowIfCancellationRequested();

            int offset = (filter.Page - 1) * filter.PageSize;
            List<SqliteParameter> parameters = new List<SqliteParameter>();
            string whereClause = BuildSearchWhere(filter, parameters);

            DataTable countResult = await _Driver.ExecuteQueryAsync(
                "SELECT COUNT(*) AS cnt FROM telemetry_samples " + whereClause + ";",
                token,
                parameters.ToArray()).ConfigureAwait(false);

            long totalCount = GetCount(countResult);

            parameters.Add(new SqliteParameter("@limit", filter.PageSize));
            parameters.Add(new SqliteParameter("@offset", offset));

            DataTable result = await _Driver.ExecuteQueryAsync(
                "SELECT " + EntryColumns + " FROM telemetry_samples " + whereClause + " ORDER BY collectedutc DESC, id DESC LIMIT @limit OFFSET @offset;",
                token,
                parameters.ToArray()).ConfigureAwait(false);

            return new TelemetryHistorySearchResult
            {
                Data = RecordsFromTable(result),
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<TelemetryRollupResult> RollupAsync(TelemetryRollupRequest request, CancellationToken token = default)
        {
            request ??= new TelemetryRollupRequest();
            token.ThrowIfCancellationRequested();

            DateTime startUtc = request.StartUtc.ToUniversalTime();
            DateTime endUtc = request.EndUtc.ToUniversalTime();
            if (endUtc <= startUtc)
            {
                throw new ArgumentException("EndUtc must be later than StartUtc.", nameof(request));
            }

            long startUnix = new DateTimeOffset(startUtc).ToUnixTimeSeconds();
            int bucketSeconds = request.BucketMinutes * 60;
            List<SqliteParameter> parameters = new List<SqliteParameter>
            {
                new SqliteParameter("@startUtc", Converters.ToTimestamp(startUtc)),
                new SqliteParameter("@endUtc", Converters.ToTimestamp(endUtc)),
                new SqliteParameter("@startUnix", startUnix),
                new SqliteParameter("@bucketSeconds", bucketSeconds)
            };

            List<string> clauses = new List<string>
            {
                "collectedutc >= @startUtc",
                "collectedutc < @endUtc"
            };

            if (!String.IsNullOrWhiteSpace(request.Hostname))
            {
                clauses.Add("hostname = @hostname");
                parameters.Add(new SqliteParameter("@hostname", request.Hostname.Trim()));
            }

            if (!String.IsNullOrWhiteSpace(request.GpuUuid))
            {
                clauses.Add("EXISTS (SELECT 1 FROM telemetry_gpu_samples g WHERE g.sampleid = telemetry_samples.id AND g.uuid = @gpuuuid)");
                parameters.Add(new SqliteParameter("@gpuuuid", request.GpuUuid.Trim()));
            }

            string whereClause = "WHERE " + String.Join(" AND ", clauses);
            string bucketExpression = "datetime(((CAST(strftime('%s', collectedutc) AS INTEGER) - @startUnix) / @bucketSeconds) * @bucketSeconds + @startUnix, 'unixepoch')";
            string gpuMetricFilter = String.IsNullOrWhiteSpace(request.GpuUuid) ? String.Empty : " AND g.uuid = @gpuuuid";

            string sql =
                "SELECT " + bucketExpression + " AS bucketutc, COUNT(*) AS samplecount, " +
                "AVG(cpuutilizationpercent) AS avgcpu, AVG(logicalcorecount) AS avglogicalcores, " +
                "AVG(memoryutilizationpercent) AS avgmemory, AVG(memorytotalbytes) AS avgmemorytotalbytes, " +
                "AVG(memoryusedbytes) AS avgmemoryusedbytes, AVG(memoryavailablebytes) AS avgmemoryavailablebytes, " +
                "AVG(networkreceivebytespersecond) AS avgnetrx, AVG(networktransmitbytespersecond) AS avgnettx, " +
                "AVG(diskreadoperationspersecond) AS avgdiskread, AVG(diskwriteoperationspersecond) AS avgdiskwrite, " +
                "AVG(diskreadqueuedepth) AS avgdiskreadqueue, AVG(diskwritequeuedepth) AS avgdiskwritequeue, " +
                "AVG(gpudevicecount) AS avggpudevices, AVG(gpuaverageutilizationpercent) AS avggpu, " +
                "MIN(gpuaverageutilizationpercent) AS mingpu, MAX(gpuaverageutilizationpercent) AS maxgpu, " +
                "AVG(gpuaveragememoryutilizationpercent) AS avggpumem, " +
                "AVG((SELECT SUM(g.memoryusedmegabytes) FROM telemetry_gpu_samples g WHERE g.sampleid = telemetry_samples.id" + gpuMetricFilter + ")) AS avggpumemusedmb, " +
                "AVG((SELECT SUM(g.memorytotalmegabytes) FROM telemetry_gpu_samples g WHERE g.sampleid = telemetry_samples.id" + gpuMetricFilter + ")) AS avggpumemtotalmb, " +
                "AVG(gpuaveragetemperaturecelsius) AS avggputemp, MIN(gpuaveragetemperaturecelsius) AS mingputemp, MAX(gpuaveragetemperaturecelsius) AS maxgputemp, " +
                "AVG(gputotalpowerusagewatts) AS avggpupower, AVG(ollamaavailablemodelcount) AS avgollamaavailable, AVG(ollamaloadedmodelcount) AS avgollamaloaded, " +
                "AVG(vllmrunningrequests) AS avgvllmrunning, AVG(vllmwaitingrequests) AS avgvllmwaiting, " +
                "AVG(vllmgpucacheusagepercent) AS avgvllmgpucache, AVG(utilyzedevicecount) AS avgutilyzedevices " +
                "FROM telemetry_samples " + whereClause + " GROUP BY " + bucketExpression + " ORDER BY " + bucketExpression + ";";

            DataTable result = await _Driver.ExecuteQueryAsync(sql, token, parameters.ToArray()).ConfigureAwait(false);
            Dictionary<DateTime, TelemetryRollupBucket> bucketMap = BucketsFromTable(result, request.BucketMinutes);
            DateTime alignedStartUtc = DateTimeOffset.FromUnixTimeSeconds(startUnix).UtcDateTime;
            List<TelemetryRollupBucket> buckets = request.IncludeEmptyBuckets
                ? FillBuckets(alignedStartUtc, endUtc, request.BucketMinutes, bucketMap)
                : new List<TelemetryRollupBucket>(bucketMap.Values);

            long totalSamples = 0L;
            foreach (TelemetryRollupBucket bucket in buckets)
            {
                totalSamples += bucket.SampleCount;
            }

            return new TelemetryRollupResult
            {
                StartUtc = startUtc,
                EndUtc = endUtc,
                BucketMinutes = request.BucketMinutes,
                TotalSamples = totalSamples,
                Buckets = buckets
            };
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            token.ThrowIfCancellationRequested();

            int rows = await _Driver.ExecuteNonQueryAsync(
                "DELETE FROM telemetry_samples WHERE id = @id;",
                token,
                new SqliteParameter("@id", id)).ConfigureAwait(false);

            return rows > 0;
        }

        public async Task<long> DeleteBulkAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default)
        {
            filter ??= new TelemetryHistorySearchFilter();
            token.ThrowIfCancellationRequested();

            List<SqliteParameter> parameters = new List<SqliteParameter>();
            string whereClause = BuildSearchWhere(filter, parameters);
            return await _Driver.ExecuteNonQueryAsync(
                "DELETE FROM telemetry_samples " + whereClause + ";",
                token,
                parameters.ToArray()).ConfigureAwait(false);
        }

        public async Task<long> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            return await _Driver.ExecuteNonQueryAsync(
                "DELETE FROM telemetry_samples WHERE collectedutc < @cutoffUtc;",
                token,
                new SqliteParameter("@cutoffUtc", Converters.ToTimestamp(cutoffUtc.ToUniversalTime()))).ConfigureAwait(false);
        }

        private TelemetrySampleDetail BuildDetail(TelemetrySnapshot snapshot)
        {
            TelemetrySampleDetail detail = new TelemetrySampleDetail
            {
                Id = IdGenerator.NewTelemetrySampleId(),
                Hostname = _Hostname,
                CollectedUtc = snapshot.CollectedUtc.ToUniversalTime(),
                PersistedUtc = DateTime.UtcNow,
                HostPlatform = snapshot.HostPlatform,
                NvidiaAvailable = snapshot.NvidiaAvailable,
                OllamaAvailable = snapshot.OllamaAvailable,
                VllmAvailable = snapshot.VllmAvailable,
                UtilyzeAvailable = snapshot.UtilyzeAvailable,
                Snapshot = snapshot
            };

            if (snapshot.Cpu != null)
            {
                detail.CpuUtilizationPercent = snapshot.Cpu.UtilizationPercent;
                detail.LogicalCoreCount = snapshot.Cpu.LogicalCoreCount;
            }

            if (snapshot.Memory != null)
            {
                detail.MemoryTotalBytes = snapshot.Memory.TotalBytes;
                detail.MemoryUsedBytes = snapshot.Memory.UsedBytes;
                detail.MemoryAvailableBytes = snapshot.Memory.AvailableBytes;
                detail.MemoryUtilizationPercent = snapshot.Memory.UtilizationPercent;
            }

            if (snapshot.Network != null)
            {
                detail.NetworkReceiveBytesPerSecond = snapshot.Network.TotalReceiveBytesPerSecond;
                detail.NetworkTransmitBytesPerSecond = snapshot.Network.TotalTransmitBytesPerSecond;
            }

            if (snapshot.Disk != null)
            {
                detail.DiskReadOperationsPerSecond = snapshot.Disk.ReadOperationsPerSecond;
                detail.DiskWriteOperationsPerSecond = snapshot.Disk.WriteOperationsPerSecond;
                detail.DiskReadQueueDepth = snapshot.Disk.ReadQueueDepth;
                detail.DiskWriteQueueDepth = snapshot.Disk.WriteQueueDepth;
            }

            ApplyGpuSummary(snapshot, detail);
            ApplyRuntimeSummary(snapshot, detail);
            return detail;
        }

        private static void ApplyGpuSummary(TelemetrySnapshot snapshot, TelemetrySampleDetail detail)
        {
            if (snapshot.Gpu == null || snapshot.Gpu.Devices.Count < 1)
            {
                return;
            }

            double gpuUtilization = 0D;
            double gpuMemory = 0D;
            double gpuTemperature = 0D;
            double gpuPower = 0D;
            int gpuCount = 0;
            int gpuMemoryCount = 0;
            int gpuTemperatureCount = 0;

            foreach (GpuDeviceTelemetry device in snapshot.Gpu.Devices)
            {
                gpuCount++;
                gpuUtilization += device.Metrics.GpuUtilizationPercent;
                gpuPower += device.Metrics.PowerUsageWatts;

                if (device.Metrics.MemoryTotalMegabytes > 0D)
                {
                    gpuMemory += device.Metrics.MemoryUtilizationPercent;
                    gpuMemoryCount++;
                }

                if (device.Metrics.TemperatureCelsius > 0D)
                {
                    gpuTemperature += device.Metrics.TemperatureCelsius;
                    gpuTemperatureCount++;
                }
            }

            detail.GpuDeviceCount = gpuCount;
            detail.GpuAverageUtilizationPercent = gpuCount > 0 ? gpuUtilization / gpuCount : null;
            detail.GpuAverageMemoryUtilizationPercent = gpuMemoryCount > 0 ? gpuMemory / gpuMemoryCount : null;
            detail.GpuAverageTemperatureCelsius = gpuTemperatureCount > 0 ? gpuTemperature / gpuTemperatureCount : null;
            detail.GpuTotalPowerUsageWatts = gpuPower;
        }

        private static void ApplyRuntimeSummary(TelemetrySnapshot snapshot, TelemetrySampleDetail detail)
        {
            if (snapshot.Ollama != null)
            {
                detail.OllamaAvailableModelCount = snapshot.Ollama.AvailableModelCount;
                detail.OllamaLoadedModelCount = snapshot.Ollama.LoadedModelCount;
            }

            if (snapshot.Vllm != null && snapshot.Vllm.Summary != null)
            {
                detail.VllmRunningRequests = snapshot.Vllm.Summary.RunningRequests;
                detail.VllmWaitingRequests = snapshot.Vllm.Summary.WaitingRequests;
                detail.VllmGpuCacheUsagePercent = snapshot.Vllm.Summary.GpuCacheUsagePercent;
            }

            if (snapshot.Utilyze != null)
            {
                detail.UtilyzeDeviceCount = snapshot.Utilyze.Devices.Count;
            }
        }

        private static string BuildInsertSampleQuery()
        {
            return "INSERT INTO telemetry_samples (" + EntryColumns + ", snapshotjson) VALUES (" +
                "@id, @hostname, @collectedutc, @persistedutc, @hostplatform, @nvidiaavailable, @ollamaavailable, @vllmavailable, @utilyzeavailable, " +
                "@cpuutilizationpercent, @logicalcorecount, @memorytotalbytes, @memoryusedbytes, @memoryavailablebytes, @memoryutilizationpercent, " +
                "@networkreceivebytespersecond, @networktransmitbytespersecond, @diskreadoperationspersecond, @diskwriteoperationspersecond, " +
                "@diskreadqueuedepth, @diskwritequeuedepth, @gpudevicecount, @gpuaverageutilizationpercent, @gpuaveragememoryutilizationpercent, " +
                "@gpuaveragetemperaturecelsius, @gputotalpowerusagewatts, @ollamaavailablemodelcount, @ollamaloadedmodelcount, @vllmrunningrequests, " +
                "@vllmwaitingrequests, @vllmgpucacheusagepercent, @utilyzedevicecount, @snapshotjson);";
        }

        private static string BuildInsertGpuSampleQuery()
        {
            return "INSERT INTO telemetry_gpu_samples (" +
                "id, sampleid, hostname, collectedutc, deviceindex, uuid, busid, model, driverversion, migprofile, gpuutilizationpercent, " +
                "memoryusedmegabytes, memoryfreemegabytes, memorytotalmegabytes, memoryutilizationpercent, temperaturecelsius, powerusagewatts, " +
                "smclockmhz, memoryclockmhz, xiderrors) VALUES (" +
                "@id, @sampleid, @hostname, @collectedutc, @deviceindex, @uuid, @busid, @model, @driverversion, @migprofile, @gpuutilizationpercent, " +
                "@memoryusedmegabytes, @memoryfreemegabytes, @memorytotalmegabytes, @memoryutilizationpercent, @temperaturecelsius, @powerusagewatts, " +
                "@smclockmhz, @memoryclockmhz, @xiderrors);";
        }

        private static SqliteParameter[] BuildSampleParameters(TelemetrySampleDetail detail, string snapshotJson)
        {
            return new SqliteParameter[]
            {
                Parameter("@id", detail.Id),
                Parameter("@hostname", detail.Hostname),
                Parameter("@collectedutc", Converters.ToTimestamp(detail.CollectedUtc)),
                Parameter("@persistedutc", Converters.ToTimestamp(detail.PersistedUtc)),
                Parameter("@hostplatform", detail.HostPlatform.ToString()),
                Parameter("@nvidiaavailable", Converters.ToBoolean(detail.NvidiaAvailable)),
                Parameter("@ollamaavailable", Converters.ToBoolean(detail.OllamaAvailable)),
                Parameter("@vllmavailable", Converters.ToBoolean(detail.VllmAvailable)),
                Parameter("@utilyzeavailable", Converters.ToBoolean(detail.UtilyzeAvailable)),
                Parameter("@cpuutilizationpercent", detail.CpuUtilizationPercent),
                Parameter("@logicalcorecount", detail.LogicalCoreCount),
                Parameter("@memorytotalbytes", detail.MemoryTotalBytes),
                Parameter("@memoryusedbytes", detail.MemoryUsedBytes),
                Parameter("@memoryavailablebytes", detail.MemoryAvailableBytes),
                Parameter("@memoryutilizationpercent", detail.MemoryUtilizationPercent),
                Parameter("@networkreceivebytespersecond", detail.NetworkReceiveBytesPerSecond),
                Parameter("@networktransmitbytespersecond", detail.NetworkTransmitBytesPerSecond),
                Parameter("@diskreadoperationspersecond", detail.DiskReadOperationsPerSecond),
                Parameter("@diskwriteoperationspersecond", detail.DiskWriteOperationsPerSecond),
                Parameter("@diskreadqueuedepth", detail.DiskReadQueueDepth),
                Parameter("@diskwritequeuedepth", detail.DiskWriteQueueDepth),
                Parameter("@gpudevicecount", detail.GpuDeviceCount),
                Parameter("@gpuaverageutilizationpercent", detail.GpuAverageUtilizationPercent),
                Parameter("@gpuaveragememoryutilizationpercent", detail.GpuAverageMemoryUtilizationPercent),
                Parameter("@gpuaveragetemperaturecelsius", detail.GpuAverageTemperatureCelsius),
                Parameter("@gputotalpowerusagewatts", detail.GpuTotalPowerUsageWatts),
                Parameter("@ollamaavailablemodelcount", detail.OllamaAvailableModelCount),
                Parameter("@ollamaloadedmodelcount", detail.OllamaLoadedModelCount),
                Parameter("@vllmrunningrequests", detail.VllmRunningRequests),
                Parameter("@vllmwaitingrequests", detail.VllmWaitingRequests),
                Parameter("@vllmgpucacheusagepercent", detail.VllmGpuCacheUsagePercent),
                Parameter("@utilyzedevicecount", detail.UtilyzeDeviceCount),
                Parameter("@snapshotjson", snapshotJson)
            };
        }

        private static SqliteParameter[] BuildGpuParameters(TelemetrySampleDetail detail, GpuDeviceTelemetry device)
        {
            return new SqliteParameter[]
            {
                Parameter("@id", IdGenerator.NewTelemetryGpuSampleId()),
                Parameter("@sampleid", detail.Id),
                Parameter("@hostname", detail.Hostname),
                Parameter("@collectedutc", Converters.ToTimestamp(detail.CollectedUtc)),
                Parameter("@deviceindex", device.DeviceIndex),
                Parameter("@uuid", device.Uuid),
                Parameter("@busid", device.BusId),
                Parameter("@model", device.Model),
                Parameter("@driverversion", device.DriverVersion),
                Parameter("@migprofile", device.MigProfile),
                Parameter("@gpuutilizationpercent", device.Metrics.GpuUtilizationPercent),
                Parameter("@memoryusedmegabytes", device.Metrics.MemoryUsedMegabytes),
                Parameter("@memoryfreemegabytes", device.Metrics.MemoryFreeMegabytes),
                Parameter("@memorytotalmegabytes", device.Metrics.MemoryTotalMegabytes),
                Parameter("@memoryutilizationpercent", device.Metrics.MemoryUtilizationPercent),
                Parameter("@temperaturecelsius", device.Metrics.TemperatureCelsius),
                Parameter("@powerusagewatts", device.Metrics.PowerUsageWatts),
                Parameter("@smclockmhz", device.Metrics.SmClockMHz),
                Parameter("@memoryclockmhz", device.Metrics.MemoryClockMHz),
                Parameter("@xiderrors", device.Metrics.XidErrors)
            };
        }

        private static SqliteParameter Parameter(string name, object? value)
        {
            return new SqliteParameter(name, value ?? DBNull.Value);
        }

        private static TelemetrySampleDetail CopyToDetail(TelemetrySampleRecord record, TelemetrySnapshot snapshot)
        {
            return new TelemetrySampleDetail
            {
                Id = record.Id,
                Hostname = record.Hostname,
                CollectedUtc = record.CollectedUtc,
                PersistedUtc = record.PersistedUtc,
                HostPlatform = record.HostPlatform,
                NvidiaAvailable = record.NvidiaAvailable,
                OllamaAvailable = record.OllamaAvailable,
                VllmAvailable = record.VllmAvailable,
                UtilyzeAvailable = record.UtilyzeAvailable,
                CpuUtilizationPercent = record.CpuUtilizationPercent,
                LogicalCoreCount = record.LogicalCoreCount,
                MemoryTotalBytes = record.MemoryTotalBytes,
                MemoryUsedBytes = record.MemoryUsedBytes,
                MemoryAvailableBytes = record.MemoryAvailableBytes,
                MemoryUtilizationPercent = record.MemoryUtilizationPercent,
                NetworkReceiveBytesPerSecond = record.NetworkReceiveBytesPerSecond,
                NetworkTransmitBytesPerSecond = record.NetworkTransmitBytesPerSecond,
                DiskReadOperationsPerSecond = record.DiskReadOperationsPerSecond,
                DiskWriteOperationsPerSecond = record.DiskWriteOperationsPerSecond,
                DiskReadQueueDepth = record.DiskReadQueueDepth,
                DiskWriteQueueDepth = record.DiskWriteQueueDepth,
                GpuDeviceCount = record.GpuDeviceCount,
                GpuAverageUtilizationPercent = record.GpuAverageUtilizationPercent,
                GpuAverageMemoryUtilizationPercent = record.GpuAverageMemoryUtilizationPercent,
                GpuAverageTemperatureCelsius = record.GpuAverageTemperatureCelsius,
                GpuTotalPowerUsageWatts = record.GpuTotalPowerUsageWatts,
                OllamaAvailableModelCount = record.OllamaAvailableModelCount,
                OllamaLoadedModelCount = record.OllamaLoadedModelCount,
                VllmRunningRequests = record.VllmRunningRequests,
                VllmWaitingRequests = record.VllmWaitingRequests,
                VllmGpuCacheUsagePercent = record.VllmGpuCacheUsagePercent,
                UtilyzeDeviceCount = record.UtilyzeDeviceCount,
                Snapshot = snapshot
            };
        }

        private static List<TelemetrySampleRecord> RecordsFromTable(DataTable table)
        {
            List<TelemetrySampleRecord> records = new List<TelemetrySampleRecord>();
            foreach (DataRow row in table.Rows)
            {
                records.Add(Converters.TelemetrySampleRecordFromDataRow(row));
            }

            return records;
        }

        private static long GetCount(DataTable table)
        {
            if (table.Rows.Count < 1 || table.Rows[0]["cnt"] == DBNull.Value) return 0L;
            return Convert.ToInt64(table.Rows[0]["cnt"]);
        }

        private static string BuildEnumerationWhere(EnumerationQuery query, List<SqliteParameter> parameters)
        {
            List<string> clauses = new List<string> { "1 = 1" };

            if (!String.IsNullOrWhiteSpace(query.HostnameFilter))
            {
                clauses.Add("hostname = @hostname");
                parameters.Add(new SqliteParameter("@hostname", query.HostnameFilter.Trim()));
            }

            if (query.StartUtc.HasValue)
            {
                clauses.Add("collectedutc >= @startUtc");
                parameters.Add(new SqliteParameter("@startUtc", Converters.ToTimestamp(query.StartUtc.Value.ToUniversalTime())));
            }

            if (query.EndUtc.HasValue)
            {
                clauses.Add("collectedutc <= @endUtc");
                parameters.Add(new SqliteParameter("@endUtc", Converters.ToTimestamp(query.EndUtc.Value.ToUniversalTime())));
            }

            return "WHERE " + String.Join(" AND ", clauses);
        }

        private static string BuildSearchWhere(TelemetryHistorySearchFilter filter, List<SqliteParameter> parameters)
        {
            List<string> clauses = new List<string> { "1 = 1" };

            AddStringClause(clauses, parameters, "hostname", "@hostname", filter.Hostname, false);
            if (filter.HostPlatform.HasValue)
            {
                clauses.Add("hostplatform = @hostplatform");
                parameters.Add(new SqliteParameter("@hostplatform", filter.HostPlatform.Value.ToString()));
            }
            AddBooleanClause(clauses, parameters, "nvidiaavailable", "@nvidiaavailable", filter.NvidiaAvailable);
            AddBooleanClause(clauses, parameters, "ollamaavailable", "@ollamaavailable", filter.OllamaAvailable);
            AddBooleanClause(clauses, parameters, "vllmavailable", "@vllmavailable", filter.VllmAvailable);
            AddBooleanClause(clauses, parameters, "utilyzeavailable", "@utilyzeavailable", filter.UtilyzeAvailable);
            AddDateClauses(clauses, parameters, filter.StartUtc, filter.EndUtc);
            AddRangeClause(clauses, parameters, "cpuutilizationpercent", "@mincpu", ">=", filter.MinCpuUtilizationPercent);
            AddRangeClause(clauses, parameters, "cpuutilizationpercent", "@maxcpu", "<=", filter.MaxCpuUtilizationPercent);
            AddRangeClause(clauses, parameters, "memoryutilizationpercent", "@minmemory", ">=", filter.MinMemoryUtilizationPercent);
            AddRangeClause(clauses, parameters, "memoryutilizationpercent", "@maxmemory", "<=", filter.MaxMemoryUtilizationPercent);
            AddRangeClause(clauses, parameters, "gpuaverageutilizationpercent", "@mingpu", ">=", filter.MinGpuUtilizationPercent);
            AddRangeClause(clauses, parameters, "gpuaverageutilizationpercent", "@maxgpu", "<=", filter.MaxGpuUtilizationPercent);
            AddRangeClause(clauses, parameters, "gpuaveragetemperaturecelsius", "@mingputemp", ">=", filter.MinGpuTemperatureCelsius);
            AddRangeClause(clauses, parameters, "gpuaveragetemperaturecelsius", "@maxgputemp", "<=", filter.MaxGpuTemperatureCelsius);

            if (!String.IsNullOrWhiteSpace(filter.GpuUuid))
            {
                clauses.Add("EXISTS (SELECT 1 FROM telemetry_gpu_samples g WHERE g.sampleid = telemetry_samples.id AND g.uuid = @gpuuuid)");
                parameters.Add(new SqliteParameter("@gpuuuid", filter.GpuUuid.Trim()));
            }

            if (!String.IsNullOrWhiteSpace(filter.GpuModel))
            {
                clauses.Add("EXISTS (SELECT 1 FROM telemetry_gpu_samples g WHERE g.sampleid = telemetry_samples.id AND LOWER(g.model) LIKE @gpumodel)");
                parameters.Add(new SqliteParameter("@gpumodel", "%" + filter.GpuModel.Trim().ToLowerInvariant() + "%"));
            }

            return "WHERE " + String.Join(" AND ", clauses);
        }

        private static void AddStringClause(List<string> clauses, List<SqliteParameter> parameters, string column, string parameterName, string? value, bool like)
        {
            if (String.IsNullOrWhiteSpace(value)) return;
            clauses.Add(like ? "LOWER(" + column + ") LIKE " + parameterName : column + " = " + parameterName);
            parameters.Add(new SqliteParameter(parameterName, like ? "%" + value.Trim().ToLowerInvariant() + "%" : value.Trim()));
        }

        private static void AddBooleanClause(List<string> clauses, List<SqliteParameter> parameters, string column, string parameterName, bool? value)
        {
            if (!value.HasValue) return;
            clauses.Add(column + " = " + parameterName);
            parameters.Add(new SqliteParameter(parameterName, Converters.ToBoolean(value.Value)));
        }

        private static void AddDateClauses(List<string> clauses, List<SqliteParameter> parameters, DateTime? startUtc, DateTime? endUtc)
        {
            if (startUtc.HasValue)
            {
                clauses.Add("collectedutc >= @startUtc");
                parameters.Add(new SqliteParameter("@startUtc", Converters.ToTimestamp(startUtc.Value.ToUniversalTime())));
            }

            if (endUtc.HasValue)
            {
                clauses.Add("collectedutc <= @endUtc");
                parameters.Add(new SqliteParameter("@endUtc", Converters.ToTimestamp(endUtc.Value.ToUniversalTime())));
            }
        }

        private static void AddRangeClause(List<string> clauses, List<SqliteParameter> parameters, string column, string parameterName, string comparison, double? value)
        {
            if (!value.HasValue) return;
            clauses.Add(column + " " + comparison + " " + parameterName);
            parameters.Add(new SqliteParameter(parameterName, value.Value));
        }

        private static int ParseContinuationToken(string? token)
        {
            if (String.IsNullOrWhiteSpace(token)) return 0;
            if (Int32.TryParse(token, out int offset) && offset >= 0)
            {
                return offset;
            }

            return 0;
        }

        private static Dictionary<DateTime, TelemetryRollupBucket> BucketsFromTable(DataTable table, int bucketMinutes)
        {
            Dictionary<DateTime, TelemetryRollupBucket> buckets = new Dictionary<DateTime, TelemetryRollupBucket>();

            foreach (DataRow row in table.Rows)
            {
                DateTime bucketStart = Converters.FromTimestamp(Converters.GetString(row, "bucketutc"));
                TelemetryRollupBucket bucket = new TelemetryRollupBucket
                {
                    BucketStartUtc = bucketStart,
                    BucketEndUtc = bucketStart.AddMinutes(bucketMinutes),
                    SampleCount = Converters.GetNullableLong(row, "samplecount") ?? 0L,
                    AverageCpuUtilizationPercent = Converters.GetNullableDouble(row, "avgcpu"),
                    AverageLogicalCoreCount = Converters.GetNullableDouble(row, "avglogicalcores"),
                    AverageMemoryUtilizationPercent = Converters.GetNullableDouble(row, "avgmemory"),
                    AverageMemoryTotalBytes = Converters.GetNullableDouble(row, "avgmemorytotalbytes"),
                    AverageMemoryUsedBytes = Converters.GetNullableDouble(row, "avgmemoryusedbytes"),
                    AverageMemoryAvailableBytes = Converters.GetNullableDouble(row, "avgmemoryavailablebytes"),
                    AverageNetworkReceiveBytesPerSecond = Converters.GetNullableDouble(row, "avgnetrx"),
                    AverageNetworkTransmitBytesPerSecond = Converters.GetNullableDouble(row, "avgnettx"),
                    AverageDiskReadOperationsPerSecond = Converters.GetNullableDouble(row, "avgdiskread"),
                    AverageDiskWriteOperationsPerSecond = Converters.GetNullableDouble(row, "avgdiskwrite"),
                    AverageDiskReadQueueDepth = Converters.GetNullableDouble(row, "avgdiskreadqueue"),
                    AverageDiskWriteQueueDepth = Converters.GetNullableDouble(row, "avgdiskwritequeue"),
                    AverageGpuDeviceCount = Converters.GetNullableDouble(row, "avggpudevices"),
                    AverageGpuUtilizationPercent = Converters.GetNullableDouble(row, "avggpu"),
                    MinGpuUtilizationPercent = Converters.GetNullableDouble(row, "mingpu"),
                    MaxGpuUtilizationPercent = Converters.GetNullableDouble(row, "maxgpu"),
                    AverageGpuMemoryUtilizationPercent = Converters.GetNullableDouble(row, "avggpumem"),
                    AverageGpuMemoryUsedMegabytes = Converters.GetNullableDouble(row, "avggpumemusedmb"),
                    AverageGpuMemoryTotalMegabytes = Converters.GetNullableDouble(row, "avggpumemtotalmb"),
                    AverageGpuTemperatureCelsius = Converters.GetNullableDouble(row, "avggputemp"),
                    MinGpuTemperatureCelsius = Converters.GetNullableDouble(row, "mingputemp"),
                    MaxGpuTemperatureCelsius = Converters.GetNullableDouble(row, "maxgputemp"),
                    AverageGpuPowerUsageWatts = Converters.GetNullableDouble(row, "avggpupower"),
                    AverageOllamaAvailableModelCount = Converters.GetNullableDouble(row, "avgollamaavailable"),
                    AverageOllamaLoadedModelCount = Converters.GetNullableDouble(row, "avgollamaloaded"),
                    AverageVllmRunningRequests = Converters.GetNullableDouble(row, "avgvllmrunning"),
                    AverageVllmWaitingRequests = Converters.GetNullableDouble(row, "avgvllmwaiting"),
                    AverageVllmGpuCacheUsagePercent = Converters.GetNullableDouble(row, "avgvllmgpucache"),
                    AverageUtilyzeDeviceCount = Converters.GetNullableDouble(row, "avgutilyzedevices")
                };

                buckets[bucketStart] = bucket;
            }

            return buckets;
        }

        private static List<TelemetryRollupBucket> FillBuckets(DateTime startUtc, DateTime endUtc, int bucketMinutes, Dictionary<DateTime, TelemetryRollupBucket> bucketMap)
        {
            List<TelemetryRollupBucket> buckets = new List<TelemetryRollupBucket>();
            DateTime cursor = startUtc;

            while (cursor < endUtc)
            {
                DateTime bucketEnd = cursor.AddMinutes(bucketMinutes);
                if (bucketEnd > endUtc)
                {
                    bucketEnd = endUtc;
                }

                if (bucketMap.TryGetValue(cursor, out TelemetryRollupBucket? bucket))
                {
                    bucket.BucketEndUtc = bucketEnd;
                    buckets.Add(bucket);
                }
                else
                {
                    buckets.Add(new TelemetryRollupBucket
                    {
                        BucketStartUtc = cursor,
                        BucketEndUtc = bucketEnd
                    });
                }

                cursor = cursor.AddMinutes(bucketMinutes);
            }

            return buckets;
        }

        private static JsonSerializerOptions BuildJsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}
