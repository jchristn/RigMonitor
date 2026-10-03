namespace Test.Xunit
{
    /// <summary>
    /// Collection for tests that attach process-wide telemetry listeners. Runs without parallelism so
    /// listeners only observe the work of the test that owns them.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class TelemetryTestCollection
    {
        /// <summary>
        /// Collection name.
        /// </summary>
        public const string Name = "Telemetry";
    }
}
