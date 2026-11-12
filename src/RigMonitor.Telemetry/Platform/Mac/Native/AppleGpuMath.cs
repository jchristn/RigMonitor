namespace RigMonitor.Telemetry.Platform.Mac.Native
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Pure calculations over Apple native readings, kept separate from interop so they are testable on any OS.
    /// </summary>
    public static class AppleGpuMath
    {
        private static readonly string[] _IdleStates = new string[] { "OFF", "IDLE", "DOWN" };

        /// <summary>
        /// Convert an IOReport energy value to joules using its unit label.
        /// </summary>
        /// <param name="value">Raw value.</param>
        /// <param name="unit">Unit label (mJ, uJ, nJ, J).</param>
        /// <returns>Joules.</returns>
        public static double EnergyToJoules(long value, string? unit)
        {
            switch ((unit ?? String.Empty).Trim())
            {
                case "J":
                    return value;
                case "mJ":
                    return value / 1000D;
                case "uJ":
                case "µJ":
                    return value / 1000000D;
                case "nJ":
                    return value / 1000000000D;
                default:
                    return value / 1000D;
            }
        }

        /// <summary>
        /// Parse a pmgr voltage-states table: little-endian pairs of (frequency, voltage) as uint32 values.
        /// Frequencies are normalized to Hz (tables are in Hz on most chips and kHz on some).
        /// </summary>
        /// <param name="bytes">Raw property bytes.</param>
        /// <returns>Frequencies in Hz in table order.</returns>
        public static List<double> ParseVoltageStates(byte[] bytes)
        {
            List<double> raw = new List<double>();
            for (int offset = 0; offset + 8 <= bytes.Length; offset += 8)
            {
                raw.Add(BitConverter.ToUInt32(bytes, offset));
            }

            double max = raw.Count > 0 ? raw.Max() : 0D;
            double scale = max >= 10000000D ? 1D : (max >= 10000D ? 1000D : 1000000D);
            return raw.Select(value => value * scale).ToList();
        }

        /// <summary>
        /// Weighted active frequency and active residency from GPU performance-state residencies.
        /// Active states (everything except OFF/IDLE/DOWN) map in order onto the non-zero entries of the frequency table.
        /// </summary>
        /// <param name="residencies">State name and residency pairs in order.</param>
        /// <param name="frequenciesHz">DVFS table in Hz.</param>
        /// <param name="frequencyMHz">Weighted frequency in MHz over active time, or null.</param>
        /// <param name="activeResidencyPercent">Share of time in an active state, or null.</param>
        public static void ComputeFrequency(
            IReadOnlyList<KeyValuePair<string, long>> residencies,
            IReadOnlyList<double> frequenciesHz,
            out double? frequencyMHz,
            out double? activeResidencyPercent)
        {
            frequencyMHz = null;
            activeResidencyPercent = null;
            if (residencies == null || residencies.Count < 1)
            {
                return;
            }

            long total = 0L;
            List<long> active = new List<long>();
            foreach (KeyValuePair<string, long> state in residencies)
            {
                long residency = Math.Max(0L, state.Value);
                total += residency;
                if (!_IdleStates.Contains((state.Key ?? String.Empty).Trim().ToUpperInvariant()))
                {
                    active.Add(residency);
                }
            }

            if (total <= 0L)
            {
                return;
            }

            long activeTotal = active.Sum();
            activeResidencyPercent = Math.Clamp(activeTotal * 100D / total, 0D, 100D);

            List<double> table = (frequenciesHz ?? Array.Empty<double>()).Where(value => value > 0D).ToList();
            if (activeTotal <= 0L || table.Count < 1)
            {
                return;
            }

            double weighted = 0D;
            int count = Math.Min(active.Count, table.Count);
            long counted = 0L;
            for (int i = 0; i < count; i++)
            {
                weighted += active[i] * table[i];
                counted += active[i];
            }

            if (counted > 0L)
            {
                frequencyMHz = weighted / counted / 1000000D;
            }
        }

        /// <summary>
        /// Select temperature sensors for the GPU from IOHID product names. Sensors named for the GPU are preferred
        /// (source "gpuSensors"). Apple Silicon parts that expose only SoC die sensors ("PMU tdie*") fall back to those,
        /// because the GPU shares the die (source "socDie").
        /// </summary>
        /// <param name="sensors">All sensors.</param>
        /// <param name="source">gpuSensors, socDie, or null when nothing matched.</param>
        /// <returns>Selected sensor values.</returns>
        public static List<double> SelectGpuSensors(IEnumerable<KeyValuePair<string, double>>? sensors, out string? source)
        {
            source = null;
            List<double> gpu = new List<double>();
            List<double> die = new List<double>();
            if (sensors == null)
            {
                return gpu;
            }

            foreach (KeyValuePair<string, double> sensor in sensors)
            {
                string name = (sensor.Key ?? String.Empty).Trim();
                if (sensor.Value <= 0D || sensor.Value >= 150D)
                {
                    continue;
                }

                if (name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    gpu.Add(sensor.Value);
                }
                else if (name.StartsWith("PMU tdie", StringComparison.OrdinalIgnoreCase))
                {
                    die.Add(sensor.Value);
                }
            }

            if (gpu.Count > 0)
            {
                source = "gpuSensors";
                return gpu;
            }

            if (die.Count > 0)
            {
                source = "socDie";
                return die;
            }

            return gpu;
        }

        /// <summary>
        /// Whether an IOHID sensor name can contribute to GPU temperature (GPU-named or SoC die sensors).
        /// </summary>
        /// <param name="name">Sensor product name.</param>
        /// <returns>True when the sensor is relevant.</returns>
        public static bool IsCandidateSensor(string? name)
        {
            string value = (name ?? String.Empty).Trim();
            return value.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0
                || value.StartsWith("PMU tdie", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Vendor for an IOAccelerator class.
        /// </summary>
        /// <param name="ioClass">IOClass.</param>
        /// <param name="model">Model.</param>
        /// <returns>apple, amd, intel, or nvidia.</returns>
        public static string VendorForAccelerator(string? ioClass, string? model)
        {
            string value = (ioClass ?? String.Empty) + " " + (model ?? String.Empty);
            if (value.IndexOf("AGX", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "apple";
            }

            if (value.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "amd";
            }

            if (value.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "intel";
            }

            if (value.IndexOf("NVDA", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "nvidia";
            }

            return "apple";
        }
    }
}
