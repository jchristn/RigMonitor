namespace RigMonitor.Telemetry.Gpu.Amd.Native
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;

    /// <summary>
    /// ROCm AMD SMI library binding (libamd_smi.so), loaded at runtime.
    /// Structures are read from oversized zeroed buffers at their leading, ABI-stable offsets so that
    /// fields appended by newer ROCm releases never overrun managed memory.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed unsafe class AmdSmiNative : IAmdSmiNative
    {
        internal const int StatusSuccess = 0;
        internal const int StatusNotSupported = 2;
        internal const ulong InitAmdGpus = 1UL << 1;
        internal const int MemoryTypeVram = 0;
        internal const int MemoryTypeGtt = 2;
        internal const int TemperatureTypeEdge = 0;
        internal const int TemperatureTypeHotspot = 1;
        internal const int TemperatureTypeVram = 2;
        internal const int TemperatureMetricCurrent = 0;
        internal const int ClockTypeGfx = 0;
        internal const int ClockTypeMemory = 4;
        internal const int NameLength = 256;
        internal const int StructBufferBytes = 4096;

        private static readonly string[] _CandidatePaths = new string[]
        {
            "/opt/rocm/lib/libamd_smi.so",
            "/opt/rocm/lib64/libamd_smi.so",
            "/usr/lib/x86_64-linux-gnu/libamd_smi.so",
            "/usr/lib64/libamd_smi.so",
            "libamd_smi.so",
            "libamd_smi.so.25",
            "libamd_smi.so.24"
        };

        private readonly object _Lock = new object();
        private readonly string _ConfiguredPath;
        private IntPtr _Library = IntPtr.Zero;
        private bool _Initialized = false;

        private delegate* unmanaged<ulong, int> _Init;
        private delegate* unmanaged<int> _ShutDown;
        private delegate* unmanaged<byte*, int> _GetLibVersion;
        private delegate* unmanaged<uint*, IntPtr*, int> _GetSocketHandles;
        private delegate* unmanaged<IntPtr, uint*, IntPtr*, int> _GetProcessorHandles;
        private delegate* unmanaged<IntPtr, ulong*, int> _GetBdf;
        private delegate* unmanaged<IntPtr, uint*, byte*, int> _GetUuid;
        private delegate* unmanaged<IntPtr, byte*, int> _GetAsicInfo;
        private delegate* unmanaged<IntPtr, byte*, int> _GetActivity;
        private delegate* unmanaged<IntPtr, int, ulong*, int> _GetMemoryTotal;
        private delegate* unmanaged<IntPtr, int, ulong*, int> _GetMemoryUsage;
        private delegate* unmanaged<IntPtr, int, int, long*, int> _GetTemperature;
        private delegate* unmanaged<IntPtr, int, byte*, int> _GetClockInfo;
        private delegate* unmanaged<IntPtr, byte*, int> _GetTotalEccCount;
        private delegate* unmanaged<IntPtr, byte*, uint, int> _GetComputePartition;
        private delegate* unmanaged<IntPtr, byte*, uint, int> _GetMemoryPartition;

        /// <summary>
        /// Instantiate the binding.
        /// </summary>
        /// <param name="configuredPath">Explicit library path, or empty to search standard locations.</param>
        public AmdSmiNative(string? configuredPath)
        {
            _ConfiguredPath = configuredPath ?? String.Empty;
        }

        /// <summary>
        /// Path of the loaded library.
        /// </summary>
        public string? LibraryPath { get; private set; } = null;

        /// <summary>
        /// Library version.
        /// </summary>
        public string? LibraryVersion { get; private set; } = null;

        /// <summary>
        /// Load and initialize the library.
        /// </summary>
        /// <param name="error">Reason when initialization fails.</param>
        /// <returns>True when initialized.</returns>
        public bool TryInitialize(out string? error)
        {
            lock (_Lock)
            {
                error = null;
                if (_Initialized)
                {
                    return true;
                }

                if (!OperatingSystem.IsLinux())
                {
                    error = "The AMD SMI library is only used on Linux.";
                    return false;
                }

                if (!TryLoadLibrary(out error))
                {
                    return false;
                }

                if (!TryBindRequired(out error))
                {
                    NativeLibrary.Free(_Library);
                    _Library = IntPtr.Zero;
                    return false;
                }

                BindOptional();

                int status = _Init(InitAmdGpus);
                if (status != StatusSuccess)
                {
                    error = "amdsmi_init failed with status " + status.ToString(CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                _Initialized = true;
                LibraryVersion = ReadVersion();
                return true;
            }
        }

        /// <summary>
        /// Read every GPU.
        /// </summary>
        /// <returns>Per-device samples.</returns>
        public IReadOnlyList<AmdSmiDeviceSample> Sample()
        {
            lock (_Lock)
            {
                if (!_Initialized)
                {
                    throw new InvalidOperationException("The AMD SMI library is not initialized.");
                }

                List<AmdSmiDeviceSample> samples = new List<AmdSmiDeviceSample>();
                foreach (IntPtr processor in EnumerateProcessors())
                {
                    samples.Add(ReadDevice(processor, samples.Count));
                }

                return samples;
            }
        }

        /// <summary>
        /// Shut down the library.
        /// </summary>
        public void Dispose()
        {
            lock (_Lock)
            {
                if (_Initialized && _ShutDown != null)
                {
                    try
                    {
                        _ShutDown();
                    }
                    catch (Exception)
                    {
                    }
                }

                _Initialized = false;
                if (_Library != IntPtr.Zero)
                {
                    NativeLibrary.Free(_Library);
                    _Library = IntPtr.Zero;
                }
            }
        }

        private bool TryLoadLibrary(out string? error)
        {
            error = null;
            List<string> candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(_ConfiguredPath))
            {
                candidates.Add(_ConfiguredPath);
            }
            else
            {
                string? rocmPath = Environment.GetEnvironmentVariable("ROCM_PATH");
                if (!String.IsNullOrWhiteSpace(rocmPath))
                {
                    candidates.Add(Path.Combine(rocmPath, "lib", "libamd_smi.so"));
                }

                candidates.AddRange(_CandidatePaths);
            }

            foreach (string candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
                {
                    _Library = handle;
                    LibraryPath = candidate;
                    return true;
                }
            }

            error = "libamd_smi.so was not found. Install ROCm or set Telemetry.AmdSmiLibraryPath.";
            return false;
        }

        private bool TryBindRequired(out string? error)
        {
            error = null;
            try
            {
                _Init = (delegate* unmanaged<ulong, int>)Export("amdsmi_init");
                _ShutDown = (delegate* unmanaged<int>)Export("amdsmi_shut_down");
                _GetSocketHandles = (delegate* unmanaged<uint*, IntPtr*, int>)Export("amdsmi_get_socket_handles");
                _GetProcessorHandles = (delegate* unmanaged<IntPtr, uint*, IntPtr*, int>)Export("amdsmi_get_processor_handles");
                _GetActivity = (delegate* unmanaged<IntPtr, byte*, int>)Export("amdsmi_get_gpu_activity");
                _GetMemoryTotal = (delegate* unmanaged<IntPtr, int, ulong*, int>)Export("amdsmi_get_gpu_memory_total");
                _GetMemoryUsage = (delegate* unmanaged<IntPtr, int, ulong*, int>)Export("amdsmi_get_gpu_memory_usage");
                return true;
            }
            catch (EntryPointNotFoundException exception)
            {
                error = "libamd_smi.so is missing a required export: " + exception.Message;
                return false;
            }
        }

        private void BindOptional()
        {
            _GetLibVersion = (delegate* unmanaged<byte*, int>)OptionalExport("amdsmi_get_lib_version");
            _GetBdf = (delegate* unmanaged<IntPtr, ulong*, int>)OptionalExport("amdsmi_get_gpu_device_bdf");
            _GetUuid = (delegate* unmanaged<IntPtr, uint*, byte*, int>)OptionalExport("amdsmi_get_gpu_device_uuid");
            _GetAsicInfo = (delegate* unmanaged<IntPtr, byte*, int>)OptionalExport("amdsmi_get_gpu_asic_info");
            _GetTemperature = (delegate* unmanaged<IntPtr, int, int, long*, int>)OptionalExport("amdsmi_get_temp_metric");
            _GetClockInfo = (delegate* unmanaged<IntPtr, int, byte*, int>)OptionalExport("amdsmi_get_clock_info");
            _GetTotalEccCount = (delegate* unmanaged<IntPtr, byte*, int>)OptionalExport("amdsmi_get_gpu_total_ecc_count");
            _GetComputePartition = (delegate* unmanaged<IntPtr, byte*, uint, int>)OptionalExport("amdsmi_get_gpu_compute_partition");
            _GetMemoryPartition = (delegate* unmanaged<IntPtr, byte*, uint, int>)OptionalExport("amdsmi_get_gpu_memory_partition");
        }

        private IntPtr Export(string name)
        {
            return NativeLibrary.GetExport(_Library, name);
        }

        private IntPtr OptionalExport(string name)
        {
            return NativeLibrary.TryGetExport(_Library, name, out IntPtr address) ? address : IntPtr.Zero;
        }

        private string? ReadVersion()
        {
            if (_GetLibVersion == null)
            {
                return null;
            }

            byte* buffer = stackalloc byte[256];
            new Span<byte>(buffer, 256).Clear();
            if (_GetLibVersion(buffer) != StatusSuccess)
            {
                return null;
            }

            uint first = *(uint*)buffer;
            uint second = *(uint*)(buffer + 4);
            uint third = *(uint*)(buffer + 8);
            uint fourth = *(uint*)(buffer + 12);

            // Older releases lead with a year field; newer releases lead with the major version.
            if (first >= 2000U)
            {
                return String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", second, third, fourth);
            }

            return String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", first, second, third);
        }

        private List<IntPtr> EnumerateProcessors()
        {
            List<IntPtr> processors = new List<IntPtr>();
            uint socketCount = 0;
            if (_GetSocketHandles(&socketCount, null) != StatusSuccess || socketCount == 0)
            {
                return processors;
            }

            IntPtr[] sockets = new IntPtr[socketCount];
            fixed (IntPtr* socketBuffer = sockets)
            {
                if (_GetSocketHandles(&socketCount, socketBuffer) != StatusSuccess)
                {
                    return processors;
                }
            }

            for (int i = 0; i < (int)socketCount; i++)
            {
                uint processorCount = 0;
                if (_GetProcessorHandles(sockets[i], &processorCount, null) != StatusSuccess || processorCount == 0)
                {
                    continue;
                }

                IntPtr[] handles = new IntPtr[processorCount];
                fixed (IntPtr* handleBuffer = handles)
                {
                    if (_GetProcessorHandles(sockets[i], &processorCount, handleBuffer) != StatusSuccess)
                    {
                        continue;
                    }
                }

                for (int j = 0; j < (int)processorCount; j++)
                {
                    processors.Add(handles[j]);
                }
            }

            return processors;
        }

        private AmdSmiDeviceSample ReadDevice(IntPtr processor, int index)
        {
            AmdSmiDeviceSample sample = new AmdSmiDeviceSample { Index = index };
            byte[] buffer = new byte[StructBufferBytes];

            fixed (byte* raw = buffer)
            {
                if (_GetBdf != null)
                {
                    ulong bdf = 0;
                    if (_GetBdf(processor, &bdf) == StatusSuccess)
                    {
                        sample.BusId = AmdSmiValues.FormatBdf(bdf);
                    }
                }

                if (_GetUuid != null)
                {
                    Array.Clear(buffer);
                    uint length = 64;
                    if (_GetUuid(processor, &length, raw) == StatusSuccess)
                    {
                        sample.Uuid = ReadString(raw, 64);
                    }
                }

                if (_GetAsicInfo != null)
                {
                    Array.Clear(buffer);
                    if (_GetAsicInfo(processor, raw) == StatusSuccess)
                    {
                        sample.MarketName = ReadString(raw, NameLength);
                    }
                }

                if (_GetComputePartition != null)
                {
                    Array.Clear(buffer);
                    if (_GetComputePartition(processor, raw, NameLength) == StatusSuccess)
                    {
                        sample.ComputePartition = NullIfEmpty(ReadString(raw, NameLength));
                    }
                }

                if (_GetMemoryPartition != null)
                {
                    Array.Clear(buffer);
                    if (_GetMemoryPartition(processor, raw, NameLength) == StatusSuccess)
                    {
                        sample.MemoryPartition = NullIfEmpty(ReadString(raw, NameLength));
                    }
                }

                Array.Clear(buffer);
                if (_GetActivity(processor, raw) == StatusSuccess)
                {
                    sample.GfxActivityPercent = AmdSmiValues.NormalizePercent(*(uint*)raw);
                    sample.UmcActivityPercent = AmdSmiValues.NormalizePercent(*(uint*)(raw + 4));
                    sample.MmActivityPercent = AmdSmiValues.NormalizePercent(*(uint*)(raw + 8));
                }

                sample.VramTotalBytes = ReadMemory(_GetMemoryTotal, processor, MemoryTypeVram);
                sample.VramUsedBytes = ReadMemory(_GetMemoryUsage, processor, MemoryTypeVram);
                sample.GttTotalBytes = ReadMemory(_GetMemoryTotal, processor, MemoryTypeGtt);
                sample.GttUsedBytes = ReadMemory(_GetMemoryUsage, processor, MemoryTypeGtt);

                if (_GetTemperature != null)
                {
                    sample.EdgeTemperatureCelsius = ReadTemperature(processor, TemperatureTypeEdge);
                    sample.HotspotTemperatureCelsius = ReadTemperature(processor, TemperatureTypeHotspot);
                    sample.MemoryTemperatureCelsius = ReadTemperature(processor, TemperatureTypeVram);
                }

                if (_GetClockInfo != null)
                {
                    Array.Clear(buffer);
                    if (_GetClockInfo(processor, ClockTypeGfx, raw) == StatusSuccess)
                    {
                        sample.GfxClockMHz = AmdSmiValues.NormalizeClock(*(uint*)raw);
                    }

                    Array.Clear(buffer);
                    if (_GetClockInfo(processor, ClockTypeMemory, raw) == StatusSuccess)
                    {
                        sample.MemoryClockMHz = AmdSmiValues.NormalizeClock(*(uint*)raw);
                    }
                }

                if (_GetTotalEccCount != null)
                {
                    Array.Clear(buffer);
                    if (_GetTotalEccCount(processor, raw) == StatusSuccess)
                    {
                        sample.CorrectableEccErrors = (long)Math.Min(*(ulong*)raw, (ulong)Int64.MaxValue);
                        sample.UncorrectableEccErrors = (long)Math.Min(*(ulong*)(raw + 8), (ulong)Int64.MaxValue);
                    }
                }
            }

            return sample;
        }

        private static double? ReadMemory(delegate* unmanaged<IntPtr, int, ulong*, int> function, IntPtr processor, int memoryType)
        {
            ulong value = 0;
            if (function(processor, memoryType, &value) != StatusSuccess)
            {
                return null;
            }

            return value;
        }

        private double? ReadTemperature(IntPtr processor, int sensor)
        {
            long value = 0;
            if (_GetTemperature(processor, sensor, TemperatureMetricCurrent, &value) != StatusSuccess)
            {
                return null;
            }

            return AmdSmiValues.NormalizeTemperature(value);
        }

        private static string ReadString(byte* buffer, int maxLength)
        {
            int length = 0;
            while (length < maxLength && buffer[length] != 0)
            {
                length++;
            }

            return Encoding.UTF8.GetString(buffer, length).Trim();
        }

        private static string? NullIfEmpty(string value)
        {
            return String.IsNullOrWhiteSpace(value) || String.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase) ? null : value;
        }
    }

    /// <summary>
    /// Pure conversions applied to raw AMD SMI values; platform independent so they can be tested anywhere.
    /// </summary>
    internal static class AmdSmiValues
    {
        internal static string FormatBdf(ulong bdf)
        {
            ulong function = bdf & 0x7UL;
            ulong device = (bdf >> 3) & 0x1FUL;
            ulong bus = (bdf >> 8) & 0xFFUL;
            ulong domain = (bdf >> 16) & 0xFFFFFFFFFFFFUL;
            return String.Format(CultureInfo.InvariantCulture, "{0:x4}:{1:x2}:{2:x2}.{3:x1}", domain, bus, device, function);
        }

        internal static double? NormalizeTemperature(long raw)
        {
            if (raw <= 0L)
            {
                return null;
            }

            double value = raw > 1000L ? raw / 1000D : raw;
            return value > 0D && value < 200D ? value : null;
        }

        internal static double? NormalizeClock(uint raw)
        {
            return raw > 0U && raw < 20000U ? raw : null;
        }

        internal static double? NormalizePercent(uint raw)
        {
            return raw <= 100U ? raw : null;
        }
    }
}
