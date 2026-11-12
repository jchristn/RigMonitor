namespace RigMonitor.Core.Models
{
    using System;

    /// <summary>
    /// Clamping helpers for optional GPU metrics.
    /// </summary>
    public static class GpuMetricMath
    {
        /// <summary>
        /// Clamp a value to zero or above, keeping null and discarding NaN or infinity.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Clamped value or null.</returns>
        public static double? NonNegative(double? value)
        {
            if (!value.HasValue || Double.IsNaN(value.Value) || Double.IsInfinity(value.Value))
            {
                return null;
            }

            return Math.Max(0D, value.Value);
        }

        /// <summary>
        /// Clamp a percentage to 0..100, keeping null and discarding NaN or infinity.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Clamped value or null.</returns>
        public static double? Percent(double? value)
        {
            if (!value.HasValue || Double.IsNaN(value.Value) || Double.IsInfinity(value.Value))
            {
                return null;
            }

            return Math.Clamp(value.Value, 0D, 100D);
        }

        /// <summary>
        /// Clamp a counter to zero or above, keeping null.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Clamped value or null.</returns>
        public static long? NonNegative(long? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return Math.Max(0L, value.Value);
        }

        /// <summary>
        /// Convert bytes to megabytes (MiB).
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>Megabytes.</returns>
        public static double BytesToMegabytes(double bytes)
        {
            return Math.Max(0D, bytes) / 1024D / 1024D;
        }
    }
}
