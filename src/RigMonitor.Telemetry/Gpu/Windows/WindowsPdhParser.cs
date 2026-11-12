namespace RigMonitor.Telemetry.Gpu.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Aggregates Windows PDH GPU counter instances per adapter LUID.
    /// </summary>
    public static class WindowsPdhParser
    {
        private static readonly Regex _Instance = new Regex(
            "luid_(?<luid>0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+)_phys_(?<phys>[0-9]+)(_eng_(?<eng>[0-9]+)_engtype_(?<type>[A-Za-z0-9_]*))?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Aggregate "GPU Engine(*)\Utilization Percentage" instances. Values are summed across processes per engine,
        /// then the busiest engine of each type is reported, matching Task Manager.
        /// </summary>
        /// <param name="instances">Instance name and value pairs.</param>
        /// <returns>LUID (upper-case) to engine type to percent.</returns>
        public static Dictionary<string, Dictionary<string, double>> AggregateEngines(IEnumerable<KeyValuePair<string, double>> instances)
        {
            Dictionary<string, double> perEngine = new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, string> engineTypes = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, double> instance in instances)
            {
                Match match = _Instance.Match(instance.Key ?? String.Empty);
                if (!match.Success || !match.Groups["eng"].Success || Double.IsNaN(instance.Value))
                {
                    continue;
                }

                string luid = match.Groups["luid"].Value.ToUpperInvariant().Replace("0X", "0x", StringComparison.Ordinal);
                string key = luid + "|" + match.Groups["phys"].Value + "|" + match.Groups["eng"].Value;
                perEngine[key] = (perEngine.TryGetValue(key, out double existing) ? existing : 0D) + Math.Max(0D, instance.Value);
                engineTypes[key] = NormalizeEngineType(match.Groups["type"].Value);
            }

            Dictionary<string, Dictionary<string, double>> result = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, double> engine in perEngine)
            {
                string luid = engine.Key.Substring(0, engine.Key.IndexOf('|'));
                string type = engineTypes[engine.Key];
                if (!result.TryGetValue(luid, out Dictionary<string, double>? types))
                {
                    types = new Dictionary<string, double>(StringComparer.Ordinal);
                    result[luid] = types;
                }

                double value = Math.Clamp(engine.Value, 0D, 100D);
                types[type] = types.TryGetValue(type, out double current) ? Math.Max(current, value) : value;
            }

            return result;
        }

        /// <summary>
        /// Aggregate "GPU Adapter Memory(*)" instances (bytes) per LUID, summing physical adapters.
        /// </summary>
        /// <param name="instances">Instance name and value pairs.</param>
        /// <returns>LUID (upper-case) to bytes.</returns>
        public static Dictionary<string, double> AggregateAdapterMemory(IEnumerable<KeyValuePair<string, double>> instances)
        {
            Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, double> instance in instances)
            {
                Match match = _Instance.Match(instance.Key ?? String.Empty);
                if (!match.Success || match.Groups["eng"].Success || Double.IsNaN(instance.Value))
                {
                    continue;
                }

                string luid = match.Groups["luid"].Value.ToUpperInvariant().Replace("0X", "0x", StringComparison.Ordinal);
                result[luid] = (result.TryGetValue(luid, out double existing) ? existing : 0D) + Math.Max(0D, instance.Value);
            }

            return result;
        }

        /// <summary>
        /// Normalize a PDH engine type to a camelCase key.
        /// </summary>
        /// <param name="type">Engine type from the instance name.</param>
        /// <returns>Normalized key.</returns>
        public static string NormalizeEngineType(string type)
        {
            string value = (type ?? String.Empty).Trim();
            if (value.Length == 0)
            {
                return "other";
            }

            if (String.Equals(value, "3D", StringComparison.OrdinalIgnoreCase))
            {
                return "3d";
            }

            if (value.StartsWith("Compute", StringComparison.OrdinalIgnoreCase))
            {
                return "compute";
            }

            int underscore = value.IndexOf('_');
            if (underscore > 0)
            {
                value = value.Substring(0, underscore);
            }

            return Char.ToLower(value[0], CultureInfo.InvariantCulture) + value.Substring(1);
        }
    }
}
