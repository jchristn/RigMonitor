namespace RigMonitor.Core.Enums
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Enumeration result ordering.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EnumerationOrderEnum
    {
        /// <summary>
        /// Order by collection time descending.
        /// </summary>
        [EnumMember(Value = "createdDescending")]
        CreatedDescending,

        /// <summary>
        /// Order by collection time ascending.
        /// </summary>
        [EnumMember(Value = "createdAscending")]
        CreatedAscending
    }
}
