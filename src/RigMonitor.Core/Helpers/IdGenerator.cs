namespace RigMonitor.Core.Helpers
{
    using System;

    /// <summary>
    /// Helper for generating K-sortable PrettyId identifiers.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Generate a telemetry sample identifier.
        /// </summary>
        /// <returns>Telemetry sample identifier with total length no greater than 32 characters.</returns>
        public static string NewTelemetrySampleId()
        {
            return Generate(Constants.TelemetrySampleIdentifierPrefix);
        }

        /// <summary>
        /// Generate a telemetry GPU sample identifier.
        /// </summary>
        /// <returns>Telemetry GPU sample identifier with total length no greater than 32 characters.</returns>
        public static string NewTelemetryGpuSampleId()
        {
            return Generate(Constants.TelemetryGpuSampleIdentifierPrefix);
        }

        /// <summary>
        /// Generate a K-sortable PrettyId using the supplied prefix.
        /// </summary>
        /// <param name="prefix">Identifier prefix.</param>
        /// <returns>Identifier with total length no greater than 32 characters.</returns>
        public static string Generate(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            if (prefix.Length >= Constants.IdentifierLength)
            {
                throw new ArgumentException("Prefix must be shorter than the total identifier length.", nameof(prefix));
            }

            int randomPartLength = Constants.IdentifierLength - prefix.Length;
            return _Generator.GenerateKSortable(prefix, randomPartLength);
        }

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();
    }
}
