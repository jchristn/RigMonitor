# Qualcomm GPU Telemetry

RigMonitor reports Qualcomm Adreno GPUs in the `qualcomm` section of `/v1/telemetry` and in the Qualcomm section of the dashboard.

Qualcomm does not publish a management library comparable to NVML or AMD SMI for these GPUs. RigMonitor reads the operating system's own GPU interfaces, in-process. These are the interfaces Task Manager and the Linux desktop tools use.

## Windows on Snapdragon

Source: `windowsGraphicsKernel`.

| Data | Interface |
|------|-----------|
| Adapter, vendor and device id, description, LUID, driver version | DXGI |
| Memory budgets (dedicated and shared) | DXGI `IDXGIAdapter3::QueryVideoMemoryInfo` |
| Utilization per engine (3D, compute, copy, video) | PDH `\GPU Engine(*)\Utilization Percentage`, summed across processes per engine; utilization is the busiest 3D or compute engine, as in Task Manager |
| Memory in use (dedicated and shared) | PDH `\GPU Adapter Memory(*)` |
| Current and maximum frequency, temperature, memory clock | D3DKMT adapter and node performance data, when the driver publishes it |

Everything runs as a standard user. The PDH counters are sampled in the background every `Telemetry.WindowsGpuSamplerIntervalMs` (default 1000 ms), so the first snapshot after startup may show no utilization yet.

Power is not reported, because D3DKMT exposes it only as a percentage. Frequency and temperature depend on the driver populating D3DKMT performance data. When it does not, those fields are omitted rather than shown as `0`.

Run RigMonitor natively on Arm64. Install the Arm64 .NET SDK; `go.bat` and `scripts/windows/*.bat` do framework-dependent builds, which run natively when the Arm64 SDK is installed. x64 emulation works but costs more.

The same backend also reports AMD adapters on Windows in the `amd` section. Set `Telemetry.WindowsGenericGpuEnabled` to `false` to limit it to Qualcomm.

## Linux

| Source | `source` value | Data |
|--------|----------------|------|
| Upstream `drm/msm` driver | `drmMsm` | Frequency from devfreq (`/sys/class/devfreq/*.gpu`), temperature from GPU thermal zones, utilization and memory from DRM fdinfo (`/proc/*/fdinfo`) |
| Downstream `kgsl` driver | `kgsl` | Utilization, frequency, and temperature from `/sys/class/kgsl/kgsl-3d0` |

`Telemetry.QualcommSource` is `auto` (drm/msm first, then kgsl), `drmMsm`, `kgsl`, or `windows`.

### Utilization scope

Upstream `drm/msm` publishes GPU busy time per DRM client in fdinfo. RigMonitor sums it across every process it can read. A non-root user can read only its own processes, and RigMonitor deliberately does not request `CAP_SYS_PTRACE`. Each device therefore reports `utilizationScope`:

- `device`: whole-device utilization reported by the driver (kgsl, Windows)
- `allProcesses`: every process was readable
- `ownProcesses`: only RigMonitor's user's processes were readable, so utilization may be under-reported

The dashboard labels partial utilization accordingly. Utilization needs two samples, so the first snapshot after startup has none.

## Memory

Adreno GPUs use shared system memory, so devices are flagged `unifiedMemory: true`. On Windows, the common memory fields are dedicated plus shared usage against the DXGI budgets. On Linux, usage comes from fdinfo and capacity from host memory.

## Verify

```bash
curl -s http://127.0.0.1:9990/v1/capabilities | python3 -m json.tool | grep -A2 qualcomm
curl -s 'http://127.0.0.1:9990/v1/telemetry?qualcomm' | python3 -m json.tool
```

On Windows, compare utilization with Task Manager's GPU tab.

## Not covered

The Hexagon NPU and Qualcomm Cloud AI 100 accelerators are not reported yet.
