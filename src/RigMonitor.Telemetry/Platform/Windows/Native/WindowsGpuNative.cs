namespace RigMonitor.Telemetry.Platform.Windows.Native
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using RigMonitor.Telemetry.Gpu.Windows;

    /// <summary>
    /// Raw PDH counter values for one collection.
    /// </summary>
    public sealed class WindowsPdhSample
    {
        /// <summary>
        /// GPU Engine utilization instances.
        /// </summary>
        public List<KeyValuePair<string, double>> Engines { get; set; } = new List<KeyValuePair<string, double>>();

        /// <summary>
        /// GPU Adapter Memory dedicated usage instances (bytes).
        /// </summary>
        public List<KeyValuePair<string, double>> DedicatedUsage { get; set; } = new List<KeyValuePair<string, double>>();

        /// <summary>
        /// GPU Adapter Memory shared usage instances (bytes).
        /// </summary>
        public List<KeyValuePair<string, double>> SharedUsage { get; set; } = new List<KeyValuePair<string, double>>();
    }

    /// <summary>
    /// Seam over the Windows graphics kernel interfaces.
    /// </summary>
    public interface IWindowsGpuNative : IDisposable
    {
        /// <summary>
        /// Enumerate hardware adapters with descriptions, memory budgets, and D3DKMT perf data.
        /// </summary>
        /// <returns>Adapters (engine utilization and memory usage unset).</returns>
        IReadOnlyList<WindowsGpuAdapterReading> EnumerateAdapters();

        /// <summary>
        /// Open the PDH query used for engine utilization and adapter memory.
        /// </summary>
        /// <param name="error">Reason on failure.</param>
        /// <returns>True when the counters are open.</returns>
        bool TryOpenCounters(out string? error);

        /// <summary>
        /// Collect PDH counters. Rate counters need two collections before values are meaningful.
        /// </summary>
        /// <returns>Sample.</returns>
        WindowsPdhSample Collect();
    }

    /// <summary>
    /// DXGI, PDH, and D3DKMT binding. Every call sits behind a Windows platform check and fails soft.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed unsafe class WindowsGpuNative : IWindowsGpuNative
    {
        private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
        private const uint DxgiAdapterFlagSoftware = 2U;
        private const int SegmentGroupLocal = 0;
        private const int SegmentGroupNonLocal = 1;
        private const int KmtQaiNodePerfData = 61;
        private const int KmtQaiAdapterPerfData = 62;
        private const uint PdhFmtDouble = 0x00000200U;
        private const int PdhMoreData = unchecked((int)0x800007D2);

        private static Guid _IidDxgiFactory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        private static Guid _IidDxgiAdapter3 = new Guid("645967a4-1392-4310-a798-8053ce3e93fd");
        private static Guid _IidD3D10Device = new Guid("9b7e4c0f-342c-4106-a19f-4f2704f689f0");

        private readonly object _Lock = new object();
        private IntPtr _Query = IntPtr.Zero;
        private IntPtr _EngineCounter = IntPtr.Zero;
        private IntPtr _DedicatedCounter = IntPtr.Zero;
        private IntPtr _SharedCounter = IntPtr.Zero;

        /// <summary>
        /// Enumerate hardware adapters.
        /// </summary>
        /// <returns>Adapters.</returns>
        public IReadOnlyList<WindowsGpuAdapterReading> EnumerateAdapters()
        {
            List<WindowsGpuAdapterReading> adapters = new List<WindowsGpuAdapterReading>();
            if (!OperatingSystem.IsWindows())
            {
                return adapters;
            }

            IntPtr factory = IntPtr.Zero;
            Guid factoryId = _IidDxgiFactory1;
            if (CreateDXGIFactory1(&factoryId, &factory) < 0 || factory == IntPtr.Zero)
            {
                return adapters;
            }

            try
            {
                for (uint index = 0; index < 64; index++)
                {
                    IntPtr adapter = IntPtr.Zero;
                    int hr = ((delegate* unmanaged<IntPtr, uint, IntPtr*, int>)VTable(factory, 12))(factory, index, &adapter);
                    if (hr == DxgiErrorNotFound || hr < 0 || adapter == IntPtr.Zero)
                    {
                        break;
                    }

                    try
                    {
                        WindowsGpuAdapterReading? reading = ReadAdapter(adapter, adapters.Count);
                        if (reading != null)
                        {
                            adapters.Add(reading);
                        }
                    }
                    finally
                    {
                        Release(adapter);
                    }
                }
            }
            finally
            {
                Release(factory);
            }

            return adapters;
        }

        /// <summary>
        /// Open PDH counters.
        /// </summary>
        /// <param name="error">Reason on failure.</param>
        /// <returns>True when open.</returns>
        public bool TryOpenCounters(out string? error)
        {
            lock (_Lock)
            {
                error = null;
                if (_Query != IntPtr.Zero)
                {
                    return true;
                }

                if (!OperatingSystem.IsWindows())
                {
                    error = "PDH is only available on Windows.";
                    return false;
                }

                IntPtr query;
                int status = PdhOpenQueryW(null, IntPtr.Zero, &query);
                if (status != 0)
                {
                    error = "PdhOpenQuery failed with status 0x" + status.ToString("X8", CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                _Query = query;
                _EngineCounter = AddCounter(@"\GPU Engine(*)\Utilization Percentage");
                _DedicatedCounter = AddCounter(@"\GPU Adapter Memory(*)\Dedicated Usage");
                _SharedCounter = AddCounter(@"\GPU Adapter Memory(*)\Shared Usage");

                if (_EngineCounter == IntPtr.Zero)
                {
                    error = "The GPU Engine performance counters are not available.";
                    PdhCloseQuery(_Query);
                    _Query = IntPtr.Zero;
                    return false;
                }

                PdhCollectQueryData(_Query);
                return true;
            }
        }

        /// <summary>
        /// Collect PDH counters.
        /// </summary>
        /// <returns>Sample.</returns>
        public WindowsPdhSample Collect()
        {
            lock (_Lock)
            {
                WindowsPdhSample sample = new WindowsPdhSample();
                if (_Query == IntPtr.Zero || PdhCollectQueryData(_Query) != 0)
                {
                    return sample;
                }

                sample.Engines = ReadCounterArray(_EngineCounter);
                sample.DedicatedUsage = ReadCounterArray(_DedicatedCounter);
                sample.SharedUsage = ReadCounterArray(_SharedCounter);
                return sample;
            }
        }

        /// <summary>
        /// Close the PDH query.
        /// </summary>
        public void Dispose()
        {
            lock (_Lock)
            {
                if (_Query != IntPtr.Zero)
                {
                    PdhCloseQuery(_Query);
                    _Query = IntPtr.Zero;
                }
            }
        }

        private static WindowsGpuAdapterReading? ReadAdapter(IntPtr adapter, int index)
        {
            DxgiAdapterDesc1 desc = default;
            if (((delegate* unmanaged<IntPtr, DxgiAdapterDesc1*, int>)VTable(adapter, 10))(adapter, &desc) < 0)
            {
                return null;
            }

            if ((desc.Flags & DxgiAdapterFlagSoftware) != 0U)
            {
                return null;
            }

            WindowsGpuAdapterReading reading = new WindowsGpuAdapterReading
            {
                Index = index,
                VendorId = desc.VendorId,
                DeviceId = desc.DeviceId,
                Description = new string(desc.Description).TrimEnd('\0').Trim(),
                Luid = WindowsAdapterMapper.FormatLuid(desc.LuidHighPart, desc.LuidLowPart),
                DedicatedVideoMemoryBytes = desc.DedicatedVideoMemory,
                SharedSystemMemoryBytes = desc.SharedSystemMemory,
                DriverVersion = ReadDriverVersion(adapter)
            };

            ReadBudgets(adapter, reading);
            ReadPerfData(desc.LuidLowPart, desc.LuidHighPart, reading);
            return reading;
        }

        private static string ReadDriverVersion(IntPtr adapter)
        {
            long version = 0;
            Guid interfaceId = _IidD3D10Device;
            if (((delegate* unmanaged<IntPtr, Guid*, long*, int>)VTable(adapter, 9))(adapter, &interfaceId, &version) < 0)
            {
                return String.Empty;
            }

            return String.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.{2}.{3}",
                (version >> 48) & 0xFFFF,
                (version >> 32) & 0xFFFF,
                (version >> 16) & 0xFFFF,
                version & 0xFFFF);
        }

        private static void ReadBudgets(IntPtr adapter, WindowsGpuAdapterReading reading)
        {
            IntPtr adapter3 = IntPtr.Zero;
            Guid interfaceId = _IidDxgiAdapter3;
            if (((delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)VTable(adapter, 0))(adapter, &interfaceId, &adapter3) < 0 || adapter3 == IntPtr.Zero)
            {
                return;
            }

            try
            {
                DxgiQueryVideoMemoryInfo local = default;
                if (((delegate* unmanaged<IntPtr, uint, int, DxgiQueryVideoMemoryInfo*, int>)VTable(adapter3, 14))(adapter3, 0, SegmentGroupLocal, &local) >= 0)
                {
                    reading.LocalBudgetBytes = local.Budget;
                }

                DxgiQueryVideoMemoryInfo nonLocal = default;
                if (((delegate* unmanaged<IntPtr, uint, int, DxgiQueryVideoMemoryInfo*, int>)VTable(adapter3, 14))(adapter3, 0, SegmentGroupNonLocal, &nonLocal) >= 0)
                {
                    reading.NonLocalBudgetBytes = nonLocal.Budget;
                }
            }
            finally
            {
                Release(adapter3);
            }
        }

        private static void ReadPerfData(uint luidLow, int luidHigh, WindowsGpuAdapterReading reading)
        {
            try
            {
                D3dkmtOpenAdapterFromLuid open = new D3dkmtOpenAdapterFromLuid { LuidLowPart = luidLow, LuidHighPart = luidHigh };
                if (D3DKMTOpenAdapterFromLuid(&open) != 0)
                {
                    return;
                }

                try
                {
                    byte* adapterPerf = stackalloc byte[128];
                    new Span<byte>(adapterPerf, 128).Clear();
                    if (Query(open.hAdapter, KmtQaiAdapterPerfData, adapterPerf, 64))
                    {
                        ulong memoryFrequency = *(ulong*)(adapterPerf + 8);
                        uint temperature = *(uint*)(adapterPerf + 56);
                        reading.MemoryFrequencyMHz = memoryFrequency > 0UL ? memoryFrequency / 1000000D : null;
                        reading.TemperatureCelsius = temperature > 0U && temperature < 2000U ? temperature / 10D : null;
                    }

                    byte* nodePerf = stackalloc byte[128];
                    new Span<byte>(nodePerf, 128).Clear();
                    if (Query(open.hAdapter, KmtQaiNodePerfData, nodePerf, 56))
                    {
                        ulong frequency = *(ulong*)(nodePerf + 8);
                        ulong maxFrequency = *(ulong*)(nodePerf + 16);
                        reading.CurrentFrequencyMHz = frequency > 0UL ? frequency / 1000000D : null;
                        reading.MaxFrequencyMHz = maxFrequency > 0UL ? maxFrequency / 1000000D : null;
                    }
                }
                finally
                {
                    D3dkmtCloseAdapter close = new D3dkmtCloseAdapter { hAdapter = open.hAdapter };
                    D3DKMTCloseAdapter(&close);
                }
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
        }

        private static bool Query(uint adapter, int type, byte* buffer, uint size)
        {
            D3dkmtQueryAdapterInfo query = new D3dkmtQueryAdapterInfo
            {
                hAdapter = adapter,
                Type = type,
                PrivateDriverData = buffer,
                PrivateDriverDataSize = size
            };

            return D3DKMTQueryAdapterInfo(&query) == 0;
        }

        private IntPtr AddCounter(string path)
        {
            IntPtr counter;
            return PdhAddEnglishCounterW(_Query, path, IntPtr.Zero, &counter) == 0 ? counter : IntPtr.Zero;
        }

        private static List<KeyValuePair<string, double>> ReadCounterArray(IntPtr counter)
        {
            List<KeyValuePair<string, double>> values = new List<KeyValuePair<string, double>>();
            if (counter == IntPtr.Zero)
            {
                return values;
            }

            uint bufferSize = 0;
            uint itemCount = 0;
            int status = PdhGetFormattedCounterArrayW(counter, PdhFmtDouble, &bufferSize, &itemCount, IntPtr.Zero);
            if (status != PdhMoreData || bufferSize == 0)
            {
                return values;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                if (PdhGetFormattedCounterArrayW(counter, PdhFmtDouble, &bufferSize, &itemCount, buffer) != 0)
                {
                    return values;
                }

                PdhFmtCounterValueItem* items = (PdhFmtCounterValueItem*)buffer;
                for (int i = 0; i < (int)itemCount; i++)
                {
                    if (items[i].CStatus > 1U)
                    {
                        continue;
                    }

                    string? name = Marshal.PtrToStringUni(items[i].Name);
                    if (!String.IsNullOrWhiteSpace(name))
                    {
                        values.Add(new KeyValuePair<string, double>(name, items[i].DoubleValue));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return values;
        }

        private static IntPtr VTable(IntPtr instance, int slot)
        {
            return (*(IntPtr**)instance)[slot];
        }

        private static void Release(IntPtr instance)
        {
            if (instance != IntPtr.Zero)
            {
                ((delegate* unmanaged<IntPtr, uint>)VTable(instance, 2))(instance);
            }
        }

        [DllImport("dxgi.dll", ExactSpelling = true)]
        private static extern int CreateDXGIFactory1(Guid* riid, IntPtr* factory);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int D3DKMTOpenAdapterFromLuid(D3dkmtOpenAdapterFromLuid* open);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int D3DKMTQueryAdapterInfo(D3dkmtQueryAdapterInfo* query);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int D3DKMTCloseAdapter(D3dkmtCloseAdapter* close);

        [DllImport("pdh.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int PdhOpenQueryW(string? dataSource, IntPtr userData, IntPtr* query);

        [DllImport("pdh.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, IntPtr* counter);

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern int PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern int PdhGetFormattedCounterArrayW(IntPtr counter, uint format, uint* bufferSize, uint* itemCount, IntPtr itemBuffer);

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern int PdhCloseQuery(IntPtr query);

        [StructLayout(LayoutKind.Sequential)]
        internal struct DxgiAdapterDesc1
        {
            public fixed char Description[128];
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public nuint DedicatedVideoMemory;
            public nuint DedicatedSystemMemory;
            public nuint SharedSystemMemory;
            public uint LuidLowPart;
            public int LuidHighPart;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DxgiQueryVideoMemoryInfo
        {
            public ulong Budget;
            public ulong CurrentUsage;
            public ulong AvailableForReservation;
            public ulong CurrentReservation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct D3dkmtOpenAdapterFromLuid
        {
            public uint LuidLowPart;
            public int LuidHighPart;
            public uint hAdapter;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct D3dkmtQueryAdapterInfo
        {
            public uint hAdapter;
            public int Type;
            public void* PrivateDriverData;
            public uint PrivateDriverDataSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct D3dkmtCloseAdapter
        {
            public uint hAdapter;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PdhFmtCounterValueItem
        {
            public IntPtr Name;
            public uint CStatus;
            public double DoubleValue;
        }
    }
}
