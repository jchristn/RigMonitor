namespace RigMonitor.Telemetry.Platform.Mac.Native
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;

    /// <summary>
    /// In-process binding to the macOS frameworks used for Apple GPU telemetry: IOKit (accelerator statistics, pmgr DVFS table,
    /// IOHID temperature sensors), CoreFoundation, Metal and Foundation through the Objective-C runtime, IOReport (GPU energy and
    /// performance-state residency), and sysctl. Every library and symbol is resolved at runtime; anything missing disables only
    /// the feature that needs it. CoreFoundation objects follow the Create/Copy rule and are always released.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public sealed unsafe class AppleGpuNative : IAppleGpuNative
    {
        private const uint CFStringEncodingUtf8 = 0x08000100U;
        private const int CFNumberSInt64Type = 4;
        private const int CFNumberDoubleType = 13;
        private const int CFNumberSInt32Type = 3;
        private const long HidEventTypeTemperature = 15;
        private const int HidTemperatureField = 15 << 16;

        private readonly object _Lock = new object();
        private readonly IntPtr _CoreFoundation;
        private readonly IntPtr _IOKit;
        private readonly IntPtr _ObjC;
        private readonly IntPtr _LibSystem;
        private IntPtr _IOReport = IntPtr.Zero;
        private IntPtr _MetalDevice = IntPtr.Zero;
        private bool _MetalResolved = false;

        // CoreFoundation
        private readonly delegate* unmanaged<IntPtr, void> _CFRelease;
        private readonly delegate* unmanaged<IntPtr, nuint> _CFGetTypeID;
        private readonly delegate* unmanaged<nuint> _CFStringGetTypeID;
        private readonly delegate* unmanaged<nuint> _CFNumberGetTypeID;
        private readonly delegate* unmanaged<nuint> _CFBooleanGetTypeID;
        private readonly delegate* unmanaged<nuint> _CFDictionaryGetTypeID;
        private readonly delegate* unmanaged<nuint> _CFArrayGetTypeID;
        private readonly delegate* unmanaged<nuint> _CFDataGetTypeID;
        private readonly delegate* unmanaged<IntPtr, byte*, uint, IntPtr> _CFStringCreateWithCString;
        private readonly delegate* unmanaged<IntPtr, nint> _CFStringGetLength;
        private readonly delegate* unmanaged<nint, uint, nint> _CFStringGetMaximumSizeForEncoding;
        private readonly delegate* unmanaged<IntPtr, byte*, nint, uint, byte> _CFStringGetCString;
        private readonly delegate* unmanaged<IntPtr, int, void*, byte> _CFNumberGetValue;
        private readonly delegate* unmanaged<IntPtr, int, void*, IntPtr> _CFNumberCreate;
        private readonly delegate* unmanaged<IntPtr, byte> _CFBooleanGetValue;
        private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr> _CFDictionaryGetValue;
        private readonly delegate* unmanaged<IntPtr, nint> _CFDictionaryGetCount;
        private readonly delegate* unmanaged<IntPtr, IntPtr*, IntPtr*, void> _CFDictionaryGetKeysAndValues;
        private readonly delegate* unmanaged<IntPtr, nint, IntPtr, IntPtr, IntPtr> _CFDictionaryCreateMutable;
        private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr, void> _CFDictionarySetValue;
        private readonly delegate* unmanaged<IntPtr, nint> _CFArrayGetCount;
        private readonly delegate* unmanaged<IntPtr, nint, IntPtr> _CFArrayGetValueAtIndex;
        private readonly delegate* unmanaged<IntPtr, nint> _CFDataGetLength;
        private readonly delegate* unmanaged<IntPtr, byte*> _CFDataGetBytePtr;
        private readonly IntPtr _CFTypeDictionaryKeyCallBacks;
        private readonly IntPtr _CFTypeDictionaryValueCallBacks;

        // IOKit
        private readonly delegate* unmanaged<byte*, IntPtr> _IOServiceMatching;
        private readonly delegate* unmanaged<byte*, IntPtr> _IOServiceNameMatching;
        private readonly delegate* unmanaged<uint, IntPtr, uint*, int> _IOServiceGetMatchingServices;
        private readonly delegate* unmanaged<uint, uint> _IOIteratorNext;
        private readonly delegate* unmanaged<uint, int> _IOObjectRelease;
        private readonly delegate* unmanaged<uint, IntPtr*, IntPtr, uint, int> _IORegistryEntryCreateCFProperties;
        private readonly delegate* unmanaged<uint, IntPtr, IntPtr, uint, IntPtr> _IORegistryEntryCreateCFProperty;
        private readonly delegate* unmanaged<uint, byte*, int> _IORegistryEntryGetName;
        private readonly delegate* unmanaged<IntPtr, IntPtr> _IOHIDEventSystemClientCreate;
        private readonly delegate* unmanaged<IntPtr, IntPtr, int> _IOHIDEventSystemClientSetMatching;
        private readonly delegate* unmanaged<IntPtr, IntPtr> _IOHIDEventSystemClientCopyServices;
        private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr> _IOHIDServiceClientCopyProperty;
        private readonly delegate* unmanaged<IntPtr, long, int, long, IntPtr> _IOHIDServiceClientCopyEvent;
        private readonly delegate* unmanaged<IntPtr, int, double> _IOHIDEventGetFloatValue;

        // Objective-C runtime
        private readonly delegate* unmanaged<byte*, IntPtr> _objc_getClass;
        private readonly delegate* unmanaged<byte*, IntPtr> _sel_registerName;
        private readonly delegate* unmanaged<IntPtr> _objc_autoreleasePoolPush;
        private readonly delegate* unmanaged<IntPtr, void> _objc_autoreleasePoolPop;
        private readonly IntPtr _objc_msgSend;

        // sysctl
        private readonly delegate* unmanaged<byte*, void*, nuint*, void*, nuint, int> _sysctlbyname;

        // IOReport
        private delegate* unmanaged<IntPtr, IntPtr, ulong, ulong, ulong, IntPtr> _IOReportCopyChannelsInGroup;
        private delegate* unmanaged<IntPtr, IntPtr, IntPtr, void> _IOReportMergeChannels;
        private delegate* unmanaged<IntPtr, IntPtr, IntPtr*, ulong, IntPtr, IntPtr> _IOReportCreateSubscription;
        private delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr> _IOReportCreateSamples;
        private delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr> _IOReportCreateSamplesDelta;
        private delegate* unmanaged<IntPtr, IntPtr> _IOReportChannelGetGroup;
        private delegate* unmanaged<IntPtr, IntPtr> _IOReportChannelGetSubGroup;
        private delegate* unmanaged<IntPtr, IntPtr> _IOReportChannelGetChannelName;
        private delegate* unmanaged<IntPtr, IntPtr> _IOReportChannelGetUnitLabel;
        private delegate* unmanaged<IntPtr, int, long> _IOReportSimpleGetIntegerValue;
        private delegate* unmanaged<IntPtr, int> _IOReportStateGetCount;
        private delegate* unmanaged<IntPtr, int, IntPtr> _IOReportStateGetNameForIndex;
        private delegate* unmanaged<IntPtr, int, long> _IOReportStateGetResidency;
        private IntPtr _IOReportSubscription = IntPtr.Zero;
        private IntPtr _IOReportChannels = IntPtr.Zero;
        private IntPtr _IOReportPrevious = IntPtr.Zero;
        private long _IOReportPreviousTimestamp = 0;

        // IOHID cache
        private IntPtr _HidClient = IntPtr.Zero;
        private IntPtr _HidServices = IntPtr.Zero;
        private List<string> _HidServiceNames = new List<string>();

        /// <summary>
        /// Load CoreFoundation, IOKit, the Objective-C runtime, and libSystem.
        /// </summary>
        public AppleGpuNative()
        {
            if (!OperatingSystem.IsMacOS())
            {
                throw new PlatformNotSupportedException("Apple GPU telemetry requires macOS.");
            }

            _CoreFoundation = NativeLibrary.Load("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
            _IOKit = NativeLibrary.Load("/System/Library/Frameworks/IOKit.framework/IOKit");
            _ObjC = NativeLibrary.Load("/usr/lib/libobjc.A.dylib");
            _LibSystem = NativeLibrary.Load("/usr/lib/libSystem.B.dylib");

            _CFRelease = (delegate* unmanaged<IntPtr, void>)Get(_CoreFoundation, "CFRelease");
            _CFGetTypeID = (delegate* unmanaged<IntPtr, nuint>)Get(_CoreFoundation, "CFGetTypeID");
            _CFStringGetTypeID = (delegate* unmanaged<nuint>)Get(_CoreFoundation, "CFStringGetTypeID");
            _CFNumberGetTypeID = (delegate* unmanaged<nuint>)Get(_CoreFoundation, "CFNumberGetTypeID");
            _CFBooleanGetTypeID = (delegate* unmanaged<nuint>)Get(_CoreFoundation, "CFBooleanGetTypeID");
            _CFDictionaryGetTypeID = (delegate* unmanaged<nuint>)Get(_CoreFoundation, "CFDictionaryGetTypeID");
            _CFArrayGetTypeID = (delegate* unmanaged<nuint>)Get(_CoreFoundation, "CFArrayGetTypeID");
            _CFDataGetTypeID = (delegate* unmanaged<nuint>)Get(_CoreFoundation, "CFDataGetTypeID");
            _CFStringCreateWithCString = (delegate* unmanaged<IntPtr, byte*, uint, IntPtr>)Get(_CoreFoundation, "CFStringCreateWithCString");
            _CFStringGetLength = (delegate* unmanaged<IntPtr, nint>)Get(_CoreFoundation, "CFStringGetLength");
            _CFStringGetMaximumSizeForEncoding = (delegate* unmanaged<nint, uint, nint>)Get(_CoreFoundation, "CFStringGetMaximumSizeForEncoding");
            _CFStringGetCString = (delegate* unmanaged<IntPtr, byte*, nint, uint, byte>)Get(_CoreFoundation, "CFStringGetCString");
            _CFNumberGetValue = (delegate* unmanaged<IntPtr, int, void*, byte>)Get(_CoreFoundation, "CFNumberGetValue");
            _CFNumberCreate = (delegate* unmanaged<IntPtr, int, void*, IntPtr>)Get(_CoreFoundation, "CFNumberCreate");
            _CFBooleanGetValue = (delegate* unmanaged<IntPtr, byte>)Get(_CoreFoundation, "CFBooleanGetValue");
            _CFDictionaryGetValue = (delegate* unmanaged<IntPtr, IntPtr, IntPtr>)Get(_CoreFoundation, "CFDictionaryGetValue");
            _CFDictionaryGetCount = (delegate* unmanaged<IntPtr, nint>)Get(_CoreFoundation, "CFDictionaryGetCount");
            _CFDictionaryGetKeysAndValues = (delegate* unmanaged<IntPtr, IntPtr*, IntPtr*, void>)Get(_CoreFoundation, "CFDictionaryGetKeysAndValues");
            _CFDictionaryCreateMutable = (delegate* unmanaged<IntPtr, nint, IntPtr, IntPtr, IntPtr>)Get(_CoreFoundation, "CFDictionaryCreateMutable");
            _CFDictionarySetValue = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)Get(_CoreFoundation, "CFDictionarySetValue");
            _CFArrayGetCount = (delegate* unmanaged<IntPtr, nint>)Get(_CoreFoundation, "CFArrayGetCount");
            _CFArrayGetValueAtIndex = (delegate* unmanaged<IntPtr, nint, IntPtr>)Get(_CoreFoundation, "CFArrayGetValueAtIndex");
            _CFDataGetLength = (delegate* unmanaged<IntPtr, nint>)Get(_CoreFoundation, "CFDataGetLength");
            _CFDataGetBytePtr = (delegate* unmanaged<IntPtr, byte*>)Get(_CoreFoundation, "CFDataGetBytePtr");
            _CFTypeDictionaryKeyCallBacks = Get(_CoreFoundation, "kCFTypeDictionaryKeyCallBacks");
            _CFTypeDictionaryValueCallBacks = Get(_CoreFoundation, "kCFTypeDictionaryValueCallBacks");

            _IOServiceMatching = (delegate* unmanaged<byte*, IntPtr>)Get(_IOKit, "IOServiceMatching");
            _IOServiceNameMatching = (delegate* unmanaged<byte*, IntPtr>)Get(_IOKit, "IOServiceNameMatching");
            _IOServiceGetMatchingServices = (delegate* unmanaged<uint, IntPtr, uint*, int>)Get(_IOKit, "IOServiceGetMatchingServices");
            _IOIteratorNext = (delegate* unmanaged<uint, uint>)Get(_IOKit, "IOIteratorNext");
            _IOObjectRelease = (delegate* unmanaged<uint, int>)Get(_IOKit, "IOObjectRelease");
            _IORegistryEntryCreateCFProperties = (delegate* unmanaged<uint, IntPtr*, IntPtr, uint, int>)Get(_IOKit, "IORegistryEntryCreateCFProperties");
            _IORegistryEntryCreateCFProperty = (delegate* unmanaged<uint, IntPtr, IntPtr, uint, IntPtr>)Get(_IOKit, "IORegistryEntryCreateCFProperty");
            _IORegistryEntryGetName = (delegate* unmanaged<uint, byte*, int>)Get(_IOKit, "IORegistryEntryGetName");
            _IOHIDEventSystemClientCreate = (delegate* unmanaged<IntPtr, IntPtr>)TryGet(_IOKit, "IOHIDEventSystemClientCreate");
            _IOHIDEventSystemClientSetMatching = (delegate* unmanaged<IntPtr, IntPtr, int>)TryGet(_IOKit, "IOHIDEventSystemClientSetMatching");
            _IOHIDEventSystemClientCopyServices = (delegate* unmanaged<IntPtr, IntPtr>)TryGet(_IOKit, "IOHIDEventSystemClientCopyServices");
            _IOHIDServiceClientCopyProperty = (delegate* unmanaged<IntPtr, IntPtr, IntPtr>)TryGet(_IOKit, "IOHIDServiceClientCopyProperty");
            _IOHIDServiceClientCopyEvent = (delegate* unmanaged<IntPtr, long, int, long, IntPtr>)TryGet(_IOKit, "IOHIDServiceClientCopyEvent");
            _IOHIDEventGetFloatValue = (delegate* unmanaged<IntPtr, int, double>)TryGet(_IOKit, "IOHIDEventGetFloatValue");

            _objc_getClass = (delegate* unmanaged<byte*, IntPtr>)Get(_ObjC, "objc_getClass");
            _sel_registerName = (delegate* unmanaged<byte*, IntPtr>)Get(_ObjC, "sel_registerName");
            _objc_autoreleasePoolPush = (delegate* unmanaged<IntPtr>)Get(_ObjC, "objc_autoreleasePoolPush");
            _objc_autoreleasePoolPop = (delegate* unmanaged<IntPtr, void>)Get(_ObjC, "objc_autoreleasePoolPop");
            _objc_msgSend = Get(_ObjC, "objc_msgSend");

            _sysctlbyname = (delegate* unmanaged<byte*, void*, nuint*, void*, nuint, int>)Get(_LibSystem, "sysctlbyname");
        }

        /// <summary>
        /// Whether IOHID temperature symbols resolved.
        /// </summary>
        public bool HidAvailable
        {
            get
            {
                return _IOHIDEventSystemClientCreate != null
                    && _IOHIDEventSystemClientSetMatching != null
                    && _IOHIDEventSystemClientCopyServices != null
                    && _IOHIDServiceClientCopyProperty != null
                    && _IOHIDServiceClientCopyEvent != null
                    && _IOHIDEventGetFloatValue != null;
            }
        }

        /// <summary>
        /// Read IOAccelerator entries.
        /// </summary>
        /// <returns>Accelerators.</returns>
        public List<AppleAcceleratorInfo> ReadAccelerators()
        {
            List<AppleAcceleratorInfo> accelerators = new List<AppleAcceleratorInfo>();
            lock (_Lock)
            {
                foreach (uint entry in MatchServices("IOAccelerator", false))
                {
                    try
                    {
                        Dictionary<string, object?>? properties = ReadProperties(entry);
                        if (properties == null)
                        {
                            continue;
                        }

                        AppleAcceleratorInfo info = new AppleAcceleratorInfo
                        {
                            RegistryName = ReadEntryName(entry),
                            IOClass = properties.TryGetValue("IOClass", out object? ioClass) ? ioClass as string ?? String.Empty : String.Empty,
                            Model = properties.TryGetValue("model", out object? model) ? AsString(model) : String.Empty,
                            CoreCount = properties.TryGetValue("gpu-core-count", out object? cores) ? AsInt(cores) : null
                        };

                        if (properties.TryGetValue("PerformanceStatistics", out object? statistics) && statistics is Dictionary<string, object?> values)
                        {
                            foreach (KeyValuePair<string, object?> pair in values)
                            {
                                double? number = AsDouble(pair.Value);
                                if (number.HasValue)
                                {
                                    info.PerformanceStatistics[pair.Key] = number.Value;
                                }
                            }
                        }

                        accelerators.Add(info);
                    }
                    finally
                    {
                        _IOObjectRelease(entry);
                    }
                }
            }

            return accelerators;
        }

        /// <summary>
        /// Read Metal, sysctl, and pmgr values.
        /// </summary>
        /// <returns>Static info.</returns>
        public AppleGpuStaticInfo ReadStatic()
        {
            AppleGpuStaticInfo info = new AppleGpuStaticInfo();
            lock (_Lock)
            {
                info.HostMemoryBytes = ReadSysctlInt64("hw.memsize");
                info.GpuFrequenciesHz = ReadGpuFrequencies();
                ReadMetal(info);
            }

            return info;
        }

        /// <summary>
        /// Read iogpu.wired_limit_mb.
        /// </summary>
        /// <returns>Limit in megabytes, or null.</returns>
        public long? ReadWiredLimitMegabytes()
        {
            lock (_Lock)
            {
                return ReadSysctlInt64("iogpu.wired_limit_mb");
            }
        }

        /// <summary>
        /// Read NSProcessInfo.thermalState.
        /// </summary>
        /// <returns>Thermal state, or null.</returns>
        public int? ReadThermalState()
        {
            lock (_Lock)
            {
                if (!TryLoadFramework("/System/Library/Frameworks/Foundation.framework/Foundation"))
                {
                    return null;
                }

                IntPtr pool = _objc_autoreleasePoolPush();
                try
                {
                    IntPtr processInfoClass = Class("NSProcessInfo");
                    if (processInfoClass == IntPtr.Zero)
                    {
                        return null;
                    }

                    IntPtr processInfo = SendIntPtr(processInfoClass, "processInfo");
                    if (processInfo == IntPtr.Zero)
                    {
                        return null;
                    }

                    long state = SendInt64(processInfo, "thermalState");
                    return state >= 0 && state <= 3 ? (int)state : null;
                }
                finally
                {
                    _objc_autoreleasePoolPop(pool);
                }
            }
        }

        /// <summary>
        /// Subscribe to IOReport channels.
        /// </summary>
        /// <param name="error">Reason on failure.</param>
        /// <returns>True when subscribed.</returns>
        public bool TryStartIOReport(out string? error)
        {
            lock (_Lock)
            {
                error = null;
                if (_IOReportSubscription != IntPtr.Zero)
                {
                    return true;
                }

                if (!NativeLibrary.TryLoad("/usr/lib/libIOReport.dylib", out _IOReport))
                {
                    error = "libIOReport.dylib could not be loaded.";
                    return false;
                }

                IntPtr copyChannels = TryGet(_IOReport, "IOReportCopyChannelsInGroup");
                IntPtr merge = TryGet(_IOReport, "IOReportMergeChannels");
                IntPtr subscribe = TryGet(_IOReport, "IOReportCreateSubscription");
                IntPtr samples = TryGet(_IOReport, "IOReportCreateSamples");
                IntPtr delta = TryGet(_IOReport, "IOReportCreateSamplesDelta");
                IntPtr group = TryGet(_IOReport, "IOReportChannelGetGroup");
                IntPtr subgroup = TryGet(_IOReport, "IOReportChannelGetSubGroup");
                IntPtr channelName = TryGet(_IOReport, "IOReportChannelGetChannelName");
                IntPtr unit = TryGet(_IOReport, "IOReportChannelGetUnitLabel");
                IntPtr integer = TryGet(_IOReport, "IOReportSimpleGetIntegerValue");
                IntPtr stateCount = TryGet(_IOReport, "IOReportStateGetCount");
                IntPtr stateName = TryGet(_IOReport, "IOReportStateGetNameForIndex");
                IntPtr residency = TryGet(_IOReport, "IOReportStateGetResidency");

                if (copyChannels == IntPtr.Zero || merge == IntPtr.Zero || subscribe == IntPtr.Zero || samples == IntPtr.Zero
                    || delta == IntPtr.Zero || group == IntPtr.Zero || subgroup == IntPtr.Zero || channelName == IntPtr.Zero
                    || unit == IntPtr.Zero || integer == IntPtr.Zero || stateCount == IntPtr.Zero || stateName == IntPtr.Zero
                    || residency == IntPtr.Zero)
                {
                    error = "libIOReport.dylib is missing a required export.";
                    return false;
                }

                _IOReportCopyChannelsInGroup = (delegate* unmanaged<IntPtr, IntPtr, ulong, ulong, ulong, IntPtr>)copyChannels;
                _IOReportMergeChannels = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)merge;
                _IOReportCreateSubscription = (delegate* unmanaged<IntPtr, IntPtr, IntPtr*, ulong, IntPtr, IntPtr>)subscribe;
                _IOReportCreateSamples = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr>)samples;
                _IOReportCreateSamplesDelta = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr>)delta;
                _IOReportChannelGetGroup = (delegate* unmanaged<IntPtr, IntPtr>)group;
                _IOReportChannelGetSubGroup = (delegate* unmanaged<IntPtr, IntPtr>)subgroup;
                _IOReportChannelGetChannelName = (delegate* unmanaged<IntPtr, IntPtr>)channelName;
                _IOReportChannelGetUnitLabel = (delegate* unmanaged<IntPtr, IntPtr>)unit;
                _IOReportSimpleGetIntegerValue = (delegate* unmanaged<IntPtr, int, long>)integer;
                _IOReportStateGetCount = (delegate* unmanaged<IntPtr, int>)stateCount;
                _IOReportStateGetNameForIndex = (delegate* unmanaged<IntPtr, int, IntPtr>)stateName;
                _IOReportStateGetResidency = (delegate* unmanaged<IntPtr, int, long>)residency;

                IntPtr energyGroup = CreateString("Energy Model");
                IntPtr statsGroup = CreateString("GPU Stats");
                IntPtr statesSubgroup = CreateString("GPU Performance States");
                try
                {
                    IntPtr energy = _IOReportCopyChannelsInGroup(energyGroup, IntPtr.Zero, 0, 0, 0);
                    IntPtr states = _IOReportCopyChannelsInGroup(statsGroup, statesSubgroup, 0, 0, 0);
                    if (energy == IntPtr.Zero && states == IntPtr.Zero)
                    {
                        error = "IOReport exposes no GPU energy or performance-state channels.";
                        return false;
                    }

                    IntPtr channels = energy != IntPtr.Zero ? energy : states;
                    if (energy != IntPtr.Zero && states != IntPtr.Zero)
                    {
                        _IOReportMergeChannels(energy, states, IntPtr.Zero);
                        _CFRelease(states);
                    }

                    IntPtr subscribed = IntPtr.Zero;
                    IntPtr subscription = _IOReportCreateSubscription(IntPtr.Zero, channels, &subscribed, 0, IntPtr.Zero);
                    _CFRelease(channels);
                    if (subscription == IntPtr.Zero || subscribed == IntPtr.Zero)
                    {
                        error = "IOReport subscription failed.";
                        return false;
                    }

                    _IOReportSubscription = subscription;
                    _IOReportChannels = subscribed;
                    return true;
                }
                finally
                {
                    _CFRelease(energyGroup);
                    _CFRelease(statsGroup);
                    _CFRelease(statesSubgroup);
                }
            }
        }

        /// <summary>
        /// Take an IOReport sample and return the delta from the previous one.
        /// </summary>
        /// <returns>Delta, or null on the first call.</returns>
        public AppleIOReportDelta? SampleIOReport()
        {
            lock (_Lock)
            {
                if (_IOReportSubscription == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr current = _IOReportCreateSamples(_IOReportSubscription, _IOReportChannels, IntPtr.Zero);
                long now = Stopwatch.GetTimestamp();
                if (current == IntPtr.Zero)
                {
                    return null;
                }

                if (_IOReportPrevious == IntPtr.Zero)
                {
                    _IOReportPrevious = current;
                    _IOReportPreviousTimestamp = now;
                    return null;
                }

                IntPtr deltaSamples = _IOReportCreateSamplesDelta(_IOReportPrevious, current, IntPtr.Zero);
                double elapsed = (now - _IOReportPreviousTimestamp) / (double)Stopwatch.Frequency;
                _CFRelease(_IOReportPrevious);
                _IOReportPrevious = current;
                _IOReportPreviousTimestamp = now;

                if (deltaSamples == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return ParseDelta(deltaSamples, elapsed);
                }
                finally
                {
                    _CFRelease(deltaSamples);
                }
            }
        }

        /// <summary>
        /// Read IOHID temperature sensors. The event-system client, matched services, and sensor names are created once and
        /// reused, so each read only copies one temperature event per sensor.
        /// </summary>
        /// <param name="include">Optional filter on sensor product names; only matching sensors are queried.</param>
        /// <returns>Sensors, or null when unavailable.</returns>
        public List<KeyValuePair<string, double>>? ReadHidTemperatures(Func<string, bool>? include = null)
        {
            if (!HidAvailable)
            {
                return null;
            }

            lock (_Lock)
            {
                if (!EnsureHidServices())
                {
                    return null;
                }

                List<KeyValuePair<string, double>> sensors = new List<KeyValuePair<string, double>>();
                for (int i = 0; i < _HidServiceNames.Count; i++)
                {
                    if (include != null && !include(_HidServiceNames[i]))
                    {
                        continue;
                    }

                    IntPtr service = _CFArrayGetValueAtIndex(_HidServices, i);
                    if (service == IntPtr.Zero)
                    {
                        continue;
                    }

                    IntPtr hidEvent = _IOHIDServiceClientCopyEvent(service, HidEventTypeTemperature, 0, 0);
                    if (hidEvent == IntPtr.Zero)
                    {
                        continue;
                    }

                    double celsius = _IOHIDEventGetFloatValue(hidEvent, HidTemperatureField);
                    _CFRelease(hidEvent);
                    string name = _HidServiceNames[i];
                    if (!String.IsNullOrWhiteSpace(name) && celsius > 0D && celsius < 150D)
                    {
                        sensors.Add(new KeyValuePair<string, double>(name, celsius));
                    }
                }

                return sensors;
            }
        }

        private bool EnsureHidServices()
        {
            if (_HidServices != IntPtr.Zero)
            {
                return true;
            }

            IntPtr client = _IOHIDEventSystemClientCreate(IntPtr.Zero);
            if (client == IntPtr.Zero)
            {
                return false;
            }

            IntPtr matching = _CFDictionaryCreateMutable(IntPtr.Zero, 0, _CFTypeDictionaryKeyCallBacks, _CFTypeDictionaryValueCallBacks);
            IntPtr productKey = CreateString("Product");
            try
            {
                SetNumber(matching, "PrimaryUsagePage", 0xff00);
                SetNumber(matching, "PrimaryUsage", 5);
                _IOHIDEventSystemClientSetMatching(client, matching);

                IntPtr services = _IOHIDEventSystemClientCopyServices(client);
                if (services == IntPtr.Zero)
                {
                    _CFRelease(client);
                    return false;
                }

                List<string> names = new List<string>();
                nint count = _CFArrayGetCount(services);
                for (nint i = 0; i < count; i++)
                {
                    IntPtr service = _CFArrayGetValueAtIndex(services, i);
                    IntPtr product = service == IntPtr.Zero ? IntPtr.Zero : _IOHIDServiceClientCopyProperty(service, productKey);
                    names.Add(product == IntPtr.Zero ? String.Empty : ReadCFString(product));
                    if (product != IntPtr.Zero)
                    {
                        _CFRelease(product);
                    }
                }

                _HidClient = client;
                _HidServices = services;
                _HidServiceNames = names;
                return true;
            }
            finally
            {
                _CFRelease(productKey);
                _CFRelease(matching);
            }
        }

        /// <summary>
        /// Release native resources.
        /// </summary>
        public void Dispose()
        {
            lock (_Lock)
            {
                if (_IOReportPrevious != IntPtr.Zero) _CFRelease(_IOReportPrevious);
                if (_IOReportChannels != IntPtr.Zero) _CFRelease(_IOReportChannels);
                if (_IOReportSubscription != IntPtr.Zero) _CFRelease(_IOReportSubscription);
                _IOReportPrevious = IntPtr.Zero;
                _IOReportChannels = IntPtr.Zero;
                _IOReportSubscription = IntPtr.Zero;

                if (_HidServices != IntPtr.Zero) _CFRelease(_HidServices);
                if (_HidClient != IntPtr.Zero) _CFRelease(_HidClient);
                _HidServices = IntPtr.Zero;
                _HidClient = IntPtr.Zero;
                _HidServiceNames = new List<string>();

                if (_MetalDevice != IntPtr.Zero)
                {
                    SendVoid(_MetalDevice, "release");
                    _MetalDevice = IntPtr.Zero;
                }
            }
        }

        private AppleIOReportDelta ParseDelta(IntPtr deltaSamples, double elapsed)
        {
            AppleIOReportDelta delta = new AppleIOReportDelta { ElapsedSeconds = elapsed };
            IntPtr channelsKey = CreateString("IOReportChannels");
            try
            {
                IntPtr channels = _CFDictionaryGetValue(deltaSamples, channelsKey);
                if (channels == IntPtr.Zero || _CFGetTypeID(channels) != _CFArrayGetTypeID())
                {
                    return delta;
                }

                double energyJoules = 0D;
                bool energySeen = false;
                nint count = _CFArrayGetCount(channels);
                for (nint i = 0; i < count; i++)
                {
                    IntPtr item = _CFArrayGetValueAtIndex(channels, i);
                    string group = ReadCFStringOrEmpty(_IOReportChannelGetGroup(item));
                    string subgroup = ReadCFStringOrEmpty(_IOReportChannelGetSubGroup(item));
                    string name = ReadCFStringOrEmpty(_IOReportChannelGetChannelName(item));

                    if (group == "Energy Model" && name == "GPU Energy")
                    {
                        long value = _IOReportSimpleGetIntegerValue(item, 0);
                        string unit = ReadCFStringOrEmpty(_IOReportChannelGetUnitLabel(item));
                        energyJoules += AppleGpuMath.EnergyToJoules(value, unit);
                        energySeen = true;
                    }
                    else if (group == "GPU Stats" && subgroup == "GPU Performance States" && name == "GPUPH")
                    {
                        int states = _IOReportStateGetCount(item);
                        for (int s = 0; s < states; s++)
                        {
                            string stateName = ReadCFStringOrEmpty(_IOReportStateGetNameForIndex(item, s));
                            long residency = _IOReportStateGetResidency(item, s);
                            delta.GpuStateResidencies.Add(new KeyValuePair<string, long>(stateName, residency));
                        }
                    }
                }

                delta.GpuEnergyJoules = energySeen ? energyJoules : null;
                return delta;
            }
            finally
            {
                _CFRelease(channelsKey);
            }
        }

        private void ReadMetal(AppleGpuStaticInfo info)
        {
            if (!_MetalResolved)
            {
                _MetalResolved = true;
                if (NativeLibrary.TryLoad("/System/Library/Frameworks/Metal.framework/Metal", out IntPtr metal)
                    && NativeLibrary.TryGetExport(metal, "MTLCreateSystemDefaultDevice", out IntPtr create))
                {
                    _MetalDevice = ((delegate* unmanaged<IntPtr>)create)();
                }
            }

            if (_MetalDevice == IntPtr.Zero)
            {
                return;
            }

            IntPtr pool = _objc_autoreleasePoolPush();
            try
            {
                IntPtr name = SendIntPtr(_MetalDevice, "name");
                if (name != IntPtr.Zero)
                {
                    IntPtr utf8 = SendIntPtr(name, "UTF8String");
                    info.MetalDeviceName = utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
                }

                info.HasUnifiedMemory = SendBool(_MetalDevice, "hasUnifiedMemory");
                ulong workingSet = SendUInt64(_MetalDevice, "recommendedMaxWorkingSetSize");
                info.RecommendedMaxWorkingSetBytes = workingSet > 0UL ? workingSet : null;
                info.MetalFamily = ReadMetalFamily();
            }
            finally
            {
                _objc_autoreleasePoolPop(pool);
            }
        }

        private string? ReadMetalFamily()
        {
            // MTLGPUFamilyApple1 = 1001 ... AppleN = 1000 + N; MTLGPUFamilyMac2 = 2002.
            for (int family = 1015; family >= 1001; family--)
            {
                if (SendBoolWithInt64(_MetalDevice, "supportsFamily:", family))
                {
                    return "apple" + (family - 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (SendBoolWithInt64(_MetalDevice, "supportsFamily:", 2002))
            {
                return "mac2";
            }

            return null;
        }

        private List<double> ReadGpuFrequencies()
        {
            List<double> frequencies = new List<double>();
            foreach (uint entry in MatchServices("pmgr", true))
            {
                try
                {
                    if (frequencies.Count > 0)
                    {
                        continue;
                    }

                    foreach (string key in new string[] { "voltage-states9", "voltage-states9-sram" })
                    {
                        IntPtr keyString = CreateString(key);
                        IntPtr data = _IORegistryEntryCreateCFProperty(entry, keyString, IntPtr.Zero, 0);
                        _CFRelease(keyString);
                        if (data == IntPtr.Zero)
                        {
                            continue;
                        }

                        try
                        {
                            if (_CFGetTypeID(data) == _CFDataGetTypeID())
                            {
                                byte[] bytes = ReadCFData(data);
                                frequencies = AppleGpuMath.ParseVoltageStates(bytes);
                            }
                        }
                        finally
                        {
                            _CFRelease(data);
                        }

                        if (frequencies.Count > 0)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    _IOObjectRelease(entry);
                }
            }

            return frequencies;
        }

        private List<uint> MatchServices(string name, bool byName)
        {
            List<uint> entries = new List<uint>();
            byte[] bytes = Utf8(name);
            IntPtr matching;
            fixed (byte* raw = bytes)
            {
                matching = byName ? _IOServiceNameMatching(raw) : _IOServiceMatching(raw);
            }

            if (matching == IntPtr.Zero)
            {
                return entries;
            }

            uint iterator = 0;
            if (_IOServiceGetMatchingServices(0, matching, &iterator) != 0 || iterator == 0)
            {
                return entries;
            }

            try
            {
                uint entry;
                while ((entry = _IOIteratorNext(iterator)) != 0)
                {
                    entries.Add(entry);
                }
            }
            finally
            {
                _IOObjectRelease(iterator);
            }

            return entries;
        }

        private Dictionary<string, object?>? ReadProperties(uint entry)
        {
            IntPtr properties = IntPtr.Zero;
            if (_IORegistryEntryCreateCFProperties(entry, &properties, IntPtr.Zero, 0) != 0 || properties == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return ToManaged(properties, 0) as Dictionary<string, object?>;
            }
            finally
            {
                _CFRelease(properties);
            }
        }

        private string ReadEntryName(uint entry)
        {
            byte* name = stackalloc byte[128];
            new Span<byte>(name, 128).Clear();
            if (_IORegistryEntryGetName(entry, name) != 0)
            {
                return String.Empty;
            }

            return Marshal.PtrToStringUTF8((IntPtr)name) ?? String.Empty;
        }

        private object? ToManaged(IntPtr value, int depth)
        {
            if (value == IntPtr.Zero || depth > 6)
            {
                return null;
            }

            nuint type = _CFGetTypeID(value);
            if (type == _CFStringGetTypeID())
            {
                return ReadCFString(value);
            }

            if (type == _CFNumberGetTypeID())
            {
                long integer = 0;
                if (_CFNumberGetValue(value, CFNumberSInt64Type, &integer) != 0)
                {
                    return integer;
                }

                double real = 0D;
                _CFNumberGetValue(value, CFNumberDoubleType, &real);
                return real;
            }

            if (type == _CFBooleanGetTypeID())
            {
                return _CFBooleanGetValue(value) != 0;
            }

            if (type == _CFDataGetTypeID())
            {
                return ReadCFData(value);
            }

            if (type == _CFArrayGetTypeID())
            {
                List<object?> list = new List<object?>();
                nint count = _CFArrayGetCount(value);
                for (nint i = 0; i < count && i < 4096; i++)
                {
                    list.Add(ToManaged(_CFArrayGetValueAtIndex(value, i), depth + 1));
                }

                return list;
            }

            if (type == _CFDictionaryGetTypeID())
            {
                Dictionary<string, object?> dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                nint count = _CFDictionaryGetCount(value);
                if (count <= 0 || count > 4096)
                {
                    return dictionary;
                }

                IntPtr[] keys = new IntPtr[count];
                IntPtr[] values = new IntPtr[count];
                fixed (IntPtr* keyBuffer = keys)
                fixed (IntPtr* valueBuffer = values)
                {
                    _CFDictionaryGetKeysAndValues(value, keyBuffer, valueBuffer);
                }

                for (int i = 0; i < (int)count; i++)
                {
                    if (keys[i] == IntPtr.Zero || _CFGetTypeID(keys[i]) != _CFStringGetTypeID())
                    {
                        continue;
                    }

                    dictionary[ReadCFString(keys[i])] = ToManaged(values[i], depth + 1);
                }

                return dictionary;
            }

            return null;
        }

        private string ReadCFStringOrEmpty(IntPtr value)
        {
            return value == IntPtr.Zero ? String.Empty : ReadCFString(value);
        }

        private string ReadCFString(IntPtr value)
        {
            nint length = _CFStringGetLength(value);
            nint size = _CFStringGetMaximumSizeForEncoding(length, CFStringEncodingUtf8) + 1;
            if (size <= 1 || size > 1 << 20)
            {
                return String.Empty;
            }

            byte[] buffer = new byte[size];
            fixed (byte* raw = buffer)
            {
                if (_CFStringGetCString(value, raw, size, CFStringEncodingUtf8) == 0)
                {
                    return String.Empty;
                }

                return Marshal.PtrToStringUTF8((IntPtr)raw) ?? String.Empty;
            }
        }

        private byte[] ReadCFData(IntPtr data)
        {
            nint length = _CFDataGetLength(data);
            if (length <= 0 || length > 1 << 20)
            {
                return Array.Empty<byte>();
            }

            byte[] bytes = new byte[length];
            Marshal.Copy((IntPtr)_CFDataGetBytePtr(data), bytes, 0, (int)length);
            return bytes;
        }

        private IntPtr CreateString(string value)
        {
            byte[] bytes = Utf8(value);
            fixed (byte* raw = bytes)
            {
                return _CFStringCreateWithCString(IntPtr.Zero, raw, CFStringEncodingUtf8);
            }
        }

        private void SetNumber(IntPtr dictionary, string key, int value)
        {
            IntPtr keyString = CreateString(key);
            IntPtr number = _CFNumberCreate(IntPtr.Zero, CFNumberSInt32Type, &value);
            _CFDictionarySetValue(dictionary, keyString, number);
            _CFRelease(number);
            _CFRelease(keyString);
        }

        private long? ReadSysctlInt64(string name)
        {
            byte[] bytes = Utf8(name);
            long value = 0;
            nuint size = (nuint)sizeof(long);
            fixed (byte* raw = bytes)
            {
                if (_sysctlbyname(raw, &value, &size, null, 0) != 0)
                {
                    return null;
                }
            }

            if (size == 4)
            {
                return (int)value;
            }

            return value;
        }

        private bool TryLoadFramework(string path)
        {
            return NativeLibrary.TryLoad(path, out IntPtr _);
        }

        private IntPtr Class(string name)
        {
            byte[] bytes = Utf8(name);
            fixed (byte* raw = bytes)
            {
                return _objc_getClass(raw);
            }
        }

        private IntPtr Selector(string name)
        {
            byte[] bytes = Utf8(name);
            fixed (byte* raw = bytes)
            {
                return _sel_registerName(raw);
            }
        }

        private IntPtr SendIntPtr(IntPtr target, string selector)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)_objc_msgSend)(target, Selector(selector));
        }

        private long SendInt64(IntPtr target, string selector)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, long>)_objc_msgSend)(target, Selector(selector));
        }

        private ulong SendUInt64(IntPtr target, string selector)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, ulong>)_objc_msgSend)(target, Selector(selector));
        }

        private bool SendBool(IntPtr target, string selector)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, byte>)_objc_msgSend)(target, Selector(selector)) != 0;
        }

        private bool SendBoolWithInt64(IntPtr target, string selector, long argument)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, long, byte>)_objc_msgSend)(target, Selector(selector), argument) != 0;
        }

        private void SendVoid(IntPtr target, string selector)
        {
            ((delegate* unmanaged<IntPtr, IntPtr, void>)_objc_msgSend)(target, Selector(selector));
        }

        private static byte[] Utf8(string value)
        {
            byte[] bytes = new byte[Encoding.UTF8.GetByteCount(value) + 1];
            Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 0);
            return bytes;
        }

        private static string AsString(object? value)
        {
            if (value is string text) return text;
            if (value is byte[] bytes) return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            return value?.ToString() ?? String.Empty;
        }

        private static int? AsInt(object? value)
        {
            double? number = AsDouble(value);
            return number.HasValue ? (int)number.Value : null;
        }

        private static double? AsDouble(object? value)
        {
            if (value is long integer) return integer;
            if (value is double real) return real;
            if (value is bool flag) return flag ? 1D : 0D;
            return null;
        }

        private static IntPtr Get(IntPtr library, string name)
        {
            return NativeLibrary.GetExport(library, name);
        }

        private static IntPtr TryGet(IntPtr library, string name)
        {
            return NativeLibrary.TryGetExport(library, name, out IntPtr address) ? address : IntPtr.Zero;
        }
    }
}
