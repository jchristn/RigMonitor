# Apple GPU Telemetry

RigMonitor reports Apple GPUs in the `apple` section of `/v1/telemetry` and in the Apple section of the dashboard.

"Metal" is Apple's graphics and compute API, not a telemetry interface. RigMonitor reads GPU telemetry from macOS frameworks directly, inside the RigMonitor process. It does not run `ioreg`, `system_profiler`, or `powermetrics`, and it needs no `sudo`.

## Sources

| Data | Framework | Notes |
|------|-----------|-------|
| Accelerators, model, GPU core count | IOKit (`IOAccelerator` registry entries) | Public API |
| Utilization (device, renderer, tiler), GPU memory in use, allocated memory | IOKit `PerformanceStatistics` | Public API; the same values Activity Monitor shows |
| Recommended GPU working set, unified memory, Metal family | Metal (`MTLCreateSystemDefaultDevice`) through the Objective-C runtime | Public API |
| Host memory, `iogpu.wired_limit_mb` | `sysctlbyname` | Public API |
| Thermal state | Foundation `NSProcessInfo.thermalState` | Public API |
| GPU power, active frequency, active residency | IOReport (`/usr/lib/libIOReport.dylib`) | Undocumented system library; no root required |
| Temperature | IOHID temperature sensors | Undocumented API; no root required |

### Undocumented APIs

On Apple Silicon, GPU power, frequency, and temperature are available only through IOReport and IOHID, which Apple does not document. Activity Monitor and widely used monitoring tools rely on the same sources. RigMonitor uses them defensively:

- Every library and symbol is resolved at runtime. If one is missing, only the fields that need it are omitted. RigMonitor still starts, and the rest of the Apple section still works.
- `Telemetry.AppleIOReportEnabled` and `Telemetry.AppleHidSensorsEnabled` turn each source off individually if a future macOS release misbehaves.
- IOReport and IOHID are sampled in the background every `Telemetry.AppleSamplerIntervalMs` (default 1000 ms), so `/v1/telemetry` never waits on them. Readings older than three intervals are dropped rather than repeated.
- CoreFoundation objects are always released. A hardware test checks working-set growth over thousands of reads.
- `appleFeatures` in `/v1/capabilities` and `features` in the `apple` section list which sources are active. The `rigmonitor.gpu.native.feature.available` metric does the same for fleet monitoring.

### Temperature

Apple Silicon exposes no documented GPU temperature. RigMonitor prefers IOHID sensors whose names identify the GPU. On chips that publish only SoC die sensors (`PMU tdie*`, for example M5), it reports the die temperature, because the GPU shares the die, and sets `temperatureSource` to `socDie`. When temperature comes from GPU-specific sensors, `temperatureSource` is `gpuSensors`.

### Memory for model placement

Apple GPUs use unified memory. The `apple` section reports:

- `devices[].metrics.memoryUsedMegabytes`: memory the GPU currently holds
- `hostMemoryTotalMegabytes`: total host memory (also used as the device's `memoryTotalMegabytes`)
- `recommendedMaxWorkingSetMegabytes`: Metal's recommended maximum working set, which is the practical upper bound for GPU allocations such as model weights
- `wiredLimitMegabytes`: `iogpu.wired_limit_mb` when you have raised it; omitted when the OS default applies

The dashboard shows these as a single bar: in use, recommended working set, and total.

## Requirements

- macOS on Apple Silicon (M1 or later). Intel Macs are best effort: IOKit and Metal work and discrete AMD or Intel GPUs are reported with their own vendor, but IOReport and IOHID GPU data are generally unavailable, so power, frequency, and temperature are omitted.
- A native run (`go.sh`, `scripts/macos/start.sh`, or the LaunchAgent from `scripts/macos/install-at-startup.sh`). Docker Desktop runs a Linux VM with no GPU access, so the Apple section is unsupported inside containers.

## Verify

```bash
curl -s http://127.0.0.1:9990/v1/capabilities | python3 -m json.tool | grep -A2 appleFeatures
curl -s 'http://127.0.0.1:9990/v1/telemetry?apple' | python3 -m json.tool
```

The startup log contains a line such as:

```text
Apple GPU available: true (source: AppleNative, Apple M5 Max, features: ioKit, metal, processInfo, ioReport, hidSensors)
```
