namespace RigMonitor.Core.Database.Sqlite
{
    using System;
    using System.Data;
    using System.Globalization;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;

    /// <summary>
    /// SQLite conversion helpers.
    /// </summary>
    internal static class Converters
    {
        /// <summary>
        /// Convert a UTC timestamp to the SQLite timestamp format.
        /// </summary>
        /// <param name="value">Timestamp.</param>
        /// <returns>Formatted timestamp.</returns>
        internal static string ToTimestamp(DateTime value)
        {
            return value.ToUniversalTime().ToString(SqliteDatabaseDriver.TimestampFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a SQLite timestamp to UTC.
        /// </summary>
        /// <param name="value">Timestamp string.</param>
        /// <returns>UTC timestamp.</returns>
        internal static DateTime FromTimestamp(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

            DateTime parsed = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        /// <summary>
        /// Convert a boolean to SQLite integer representation.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>1 for true, 0 for false.</returns>
        internal static int ToBoolean(bool value)
        {
            return value ? 1 : 0;
        }

        /// <summary>
        /// Convert a DataRow into a telemetry sample record.
        /// </summary>
        /// <param name="row">Data row.</param>
        /// <returns>Telemetry sample record.</returns>
        internal static TelemetrySampleRecord TelemetrySampleRecordFromDataRow(DataRow row)
        {
            TelemetrySampleRecord record = new TelemetrySampleRecord
            {
                Id = GetString(row, "id"),
                Hostname = GetString(row, "hostname"),
                CollectedUtc = FromTimestamp(GetString(row, "collectedutc")),
                PersistedUtc = FromTimestamp(GetString(row, "persistedutc")),
                HostPlatform = ParseHostPlatform(GetString(row, "hostplatform")),
                NvidiaAvailable = GetBoolean(row, "nvidiaavailable"),
                OllamaAvailable = GetBoolean(row, "ollamaavailable"),
                VllmAvailable = GetBoolean(row, "vllmavailable"),
                UtilyzeAvailable = GetBoolean(row, "utilyzeavailable"),
                CpuUtilizationPercent = GetNullableDouble(row, "cpuutilizationpercent"),
                LogicalCoreCount = GetNullableInt(row, "logicalcorecount"),
                MemoryTotalBytes = GetNullableLong(row, "memorytotalbytes"),
                MemoryUsedBytes = GetNullableLong(row, "memoryusedbytes"),
                MemoryAvailableBytes = GetNullableLong(row, "memoryavailablebytes"),
                MemoryUtilizationPercent = GetNullableDouble(row, "memoryutilizationpercent"),
                NetworkReceiveBytesPerSecond = GetNullableDouble(row, "networkreceivebytespersecond"),
                NetworkTransmitBytesPerSecond = GetNullableDouble(row, "networktransmitbytespersecond"),
                DiskReadOperationsPerSecond = GetNullableDouble(row, "diskreadoperationspersecond"),
                DiskWriteOperationsPerSecond = GetNullableDouble(row, "diskwriteoperationspersecond"),
                DiskReadQueueDepth = GetNullableDouble(row, "diskreadqueuedepth"),
                DiskWriteQueueDepth = GetNullableDouble(row, "diskwritequeuedepth"),
                GpuDeviceCount = GetNullableInt(row, "gpudevicecount"),
                GpuAverageUtilizationPercent = GetNullableDouble(row, "gpuaverageutilizationpercent"),
                GpuAverageMemoryUtilizationPercent = GetNullableDouble(row, "gpuaveragememoryutilizationpercent"),
                GpuAverageTemperatureCelsius = GetNullableDouble(row, "gpuaveragetemperaturecelsius"),
                GpuTotalPowerUsageWatts = GetNullableDouble(row, "gputotalpowerusagewatts"),
                OllamaAvailableModelCount = GetNullableInt(row, "ollamaavailablemodelcount"),
                OllamaLoadedModelCount = GetNullableInt(row, "ollamaloadedmodelcount"),
                VllmRunningRequests = GetNullableDouble(row, "vllmrunningrequests"),
                VllmWaitingRequests = GetNullableDouble(row, "vllmwaitingrequests"),
                VllmGpuCacheUsagePercent = GetNullableDouble(row, "vllmgpucacheusagepercent"),
                UtilyzeDeviceCount = GetNullableInt(row, "utilyzedevicecount")
            };

            return record;
        }

        internal static string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return String.Empty;
            return row[columnName]?.ToString() ?? String.Empty;
        }

        internal static bool GetBoolean(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return false;
            return Convert.ToInt32(row[columnName], CultureInfo.InvariantCulture) != 0;
        }

        internal static int? GetNullableInt(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            return Convert.ToInt32(row[columnName], CultureInfo.InvariantCulture);
        }

        internal static long? GetNullableLong(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            return Convert.ToInt64(row[columnName], CultureInfo.InvariantCulture);
        }

        internal static double? GetNullableDouble(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            return Convert.ToDouble(row[columnName], CultureInfo.InvariantCulture);
        }

        private static HostPlatformEnum ParseHostPlatform(string value)
        {
            if (Enum.TryParse(value, true, out HostPlatformEnum result))
            {
                return result;
            }

            return HostPlatformEnum.Unknown;
        }
    }
}
