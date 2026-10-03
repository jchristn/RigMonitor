namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One metric measurement captured by <see cref="TelemetryCapture"/>.
    /// </summary>
    public class RecordedMeasurement
    {
        /// <summary>
        /// Meter name.
        /// </summary>
        public string MeterName { get; set; } = String.Empty;

        /// <summary>
        /// Instrument name.
        /// </summary>
        public string InstrumentName { get; set; } = String.Empty;

        /// <summary>
        /// Instrument unit.
        /// </summary>
        public string? Unit { get; set; } = null;

        /// <summary>
        /// Measured value.
        /// </summary>
        public double Value { get; set; } = 0D;

        /// <summary>
        /// Measurement tags.
        /// </summary>
        public Dictionary<string, string?> Tags { get; set; } = new Dictionary<string, string?>(StringComparer.Ordinal);

        /// <summary>
        /// Determine whether every supplied tag is present with the supplied value.
        /// </summary>
        /// <param name="expected">Expected tags as alternating key, value strings.</param>
        /// <returns>True when all match.</returns>
        public bool HasTags(params string[] expected)
        {
            if (expected == null) return true;
            for (int i = 0; i + 1 < expected.Length; i += 2)
            {
                if (!Tags.TryGetValue(expected[i], out string? value)) return false;
                if (!String.Equals(value, expected[i + 1], StringComparison.Ordinal)) return false;
            }

            return true;
        }
    }
}
