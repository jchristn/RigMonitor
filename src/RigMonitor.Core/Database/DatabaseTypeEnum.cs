namespace RigMonitor.Core.Database
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Database provider type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// SQLite database.
        /// </summary>
        [EnumMember(Value = "Sqlite")]
        Sqlite,

        /// <summary>
        /// MySQL database.
        /// </summary>
        [EnumMember(Value = "Mysql")]
        Mysql,

        /// <summary>
        /// PostgreSQL database.
        /// </summary>
        [EnumMember(Value = "Postgresql")]
        Postgresql,

        /// <summary>
        /// SQL Server database.
        /// </summary>
        [EnumMember(Value = "SqlServer")]
        SqlServer
    }
}
