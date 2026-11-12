# Additional GPU Telemetry Plan: AMD ROCm, Apple Silicon, and Qualcomm Adreno

Status: implemented in `0.3.0-alpha`. Differences from this plan, decided during implementation:

- **AMD power** is not read from the AMD SMI library, because `amdsmi_power_info_t` changed layout across ROCm releases. Power comes from sysfs gap-fill (`hwmon` `power1_average`/`power1_input`). Structures are read from oversized zeroed buffers at their stable leading offsets, rather than through a declared version range.
- **Apple temperature** prefers GPU-named IOHID sensors and falls back to SoC die sensors (`PMU tdie*`), reported as `temperatureSource: "socDie"`. Apple M5 exposes only die sensors. IOHID reads (about 150 ms) run in the background sampler with IOReport, not on the request path.
- **Windows power** is not reported, because D3DKMT exposes it only as a percentage.
- **Fixtures** live inline in the tests (recorded values and temporary sysfs/procfs trees) rather than under `Test.Shared/Fixtures/Gpu`. The fixture capture script is not included yet.
- **CI jobs** (macOS, Windows Arm64, Linux Arm64, nightly soak) are not added, because the repository has no CI configuration. The hardware tests in `Test.Automated` run on any developer machine and skip themselves when the hardware is absent. The leak check is a shorter in-suite test instead of a nightly soak.
- **Dashboard default** for hiding unsupported sections is served through `/v1/capabilities` (`hideUnsupportedGpuSections`), and viewers can override it per browser.
Target version: `0.3.0-alpha` (contains approved breaking API changes; see [Breaking changes](#10-breaking-changes))

## 1. Summary

RigMonitor's GPU telemetry comes only from the NVIDIA DCGM exporter today. This plan makes **NVIDIA, AMD, Apple, and Qualcomm first-class peers**:

- **Four vendor sections** in the API (`nvidia`, `amd`, `apple`, `qualcomm`). Each has its own collection status, freshness, and vendor-specific telemetry, matching how `ollama`, `vllm`, and `utilyze` work today.
- A vendor-neutral **`gpu` aggregate section**, derived from the four without extra collection calls, for summary cards, persistence, and fleet-wide rollups.
- **Four vendor sections in the dashboard** (NVIDIA, AMD, Apple, Qualcomm), each showing its full telemetry. They are integrated through the rest of the dashboard: overview stat cards, capability pills, analytics charts with a series per vendor, filters, history tables, and JSON inspection.
- **Native, in-process collection.** RigMonitor talks to the operating system and vendor libraries directly. It does not run CLI tools and parse their text output, and it never needs `sudo`:
  - **Apple:** IOKit, CoreFoundation, Metal, Foundation, IOReport, and IOHIDFamily, called through .NET P/Invoke.
  - **AMD:** the ROCm `libamd_smi` C library through P/Invoke, with direct `amdgpu` sysfs reads to fall back on and to fill gaps.
  - **Qualcomm Adreno:** on Windows on Snapdragon, the Windows graphics kernel interfaces (D3DKMT, DXGI, PDH) that Task Manager also reads. On Linux, the upstream `drm/msm` driver's devfreq, fdinfo, and thermal sysfs. Qualcomm ships no public telemetry library comparable to NVML or AMD SMI, so these OS interfaces are the robust native path.
  - **NVIDIA:** unchanged in this plan (DCGM exporter). NVML is listed as a follow-up for parity (section 13).

### Decisions recorded

| # | Question | Decision |
|---|----------|----------|
| 1 | Change `gpu.vendor` from `"NVIDIA"` to camelCase enum values | **Approved.** |
| 2 | AMD backend preference | **The most robust implementation.** Native `libamd_smi` first, with per-field gap-fill from sysfs and a full sysfs fallback. The AMD Device Metrics Exporter is supported but used only when native access is not possible (for example inside a container) or when chosen explicitly. The `amd-smi` CLI is not used. |
| 3 | How Apple telemetry is collected | **Native, inside RigMonitor, no hacks.** No `ioreg`, `system_profiler`, or `powermetrics` subprocesses and no sudoers rules. Everything goes through framework calls in-process. |
| 4 | Undocumented Apple APIs | See section 4.2. On Apple Silicon, GPU power, frequency, and temperature are **only** available through Apple frameworks that have no public documentation (IOReport, IOHIDEventSystemClient). The earlier question was whether depending on them is acceptable. Given decision 3, the plan uses them natively and defensively: they are loaded at runtime, every symbol is checked, an unavailable API makes that field `null` and never crashes, and macOS CI catches OS-update regressions. Activity Monitor, `powermetrics`, and widely used open-source monitors read the same sources. |
| 5 | Dashboard layout | **Separate NVIDIA, AMD, Apple, and Qualcomm sections**, integrated through overview, charts, filters, and tables. |
| 6 | Qualcomm | **Added** as a fourth vendor (section 4.4), delivered as its own phase (Phase 4), which can run in parallel with the AMD and Apple phases. The Windows backend is built vendor-neutral, so it also gives basic telemetry for AMD and Intel GPUs on Windows at no extra cost (section 4.4). |

## 2. Current state (what has to change)

| Area | File | NVIDIA-specific today |
|------|------|-----------------------|
| Provider contract | `src/RigMonitor.Core/Services/Interfaces/IGpuTelemetryProvider.cs` | One provider, one result. |
| DCGM provider | `src/RigMonitor.Telemetry/Services/NvidiaDcgmGpuTelemetryProvider.cs` | Contains its own Prometheus text parser. |
| DCGM client | `src/RigMonitor.Telemetry/Services/DcgmExporterClient.cs` | Generic "GET metrics text" client hard-wired to `ServiceDcgm`. |
| Models | `src/RigMonitor.Core/Models/GpuTelemetry.cs`, `GpuDeviceTelemetry.cs`, `GpuUtilizationTelemetry.cs` | `Vendor = "NVIDIA"`. Non-nullable temperature, power, clocks, and XID (all default `0`). |
| Snapshot | `src/RigMonitor.Core/Models/TelemetrySnapshot.cs`, `TelemetryCollectionMetadata.cs`, `TelemetryRequestOptions.cs` | One `Gpu` section, one `IncludeGpu` selector. |
| Capabilities | `RuntimeCapabilities.cs`, `RuntimeCapabilitiesService.cs` | Only `NvidiaAvailable` / `DcgmExporterUrl`, probed once at startup. |
| Section gating | `src/RigMonitor.Telemetry/Services/TelemetryService.cs:243-251` | `gpu` supported only when `capabilities.NvidiaAvailable`. |
| Unified memory | `TelemetryService.cs:321-358` | Model-string match on `GB10` / `DGX Spark`. |
| Request parsing | `src/RigMonitor.Server/Services/TelemetryRequestParser.cs` | No vendor selectors. |
| Settings | `src/RigMonitor.Core/Settings/TelemetrySettings.cs`, `Constants.cs` | Only `DcgmExporterUrl`. |
| Server wiring | `src/RigMonitor.Server/RigMonitorServer.cs:116-175` | Builds the DCGM client and provider directly. |
| Routes | `src/RigMonitor.Server/Routes/GeneralRoutes.cs:49` | OpenAPI text mentions DCGM only. |
| SQLite | `src/RigMonitor.Core/Database/Sqlite/Queries/SetupQueries.cs` | `nvidiaavailable`. `telemetry_gpu_samples` has no vendor/source. |
| History | `TelemetryHistoryMethods.cs`, `TelemetryHistorySearchFilter.cs`, `TelemetryRollupRequest.cs`, `TelemetryRollupBucket.cs`, `TelemetrySampleRecord.cs`, `TelemetrySampleDetail.cs` | Host-level GPU averages only. No vendor dimension. |
| Observability | `src/RigMonitor.Core/Observability/RigMonitorTelemetryNames.cs`, `src/RigMonitor.Server/Services/RigMonitorMetricCatalog.cs` | `ServiceDcgm`. Capability labels `nvidia, ollama, vllm, utilyze`. |
| Dashboard | `dashboard/src/views/HomeView.jsx:265, 285, 487-525` | One GPU card. `0 C` / `0 W` shown for missing values. |
| | `dashboard/src/views/TelemetryAnalyticsView.jsx:20-36, 90, 140, 347-427, 839` | Single-series SVG line charts. GPU descriptors use host averages. `nvidiaAvailable` filter only. |
| | `dashboard/src/i18n/locales/*.js` (9 locales) | DCGM-specific wording. |
| Docs | `README.md`, `REST_API.md`, `INSTALLING_DCGM.md`, `BARE_METAL_DEPLOYMENT.md`, `TELEMETRY.md`, `CHANGELOG.md` | NVIDIA-only. |
| Assets | `RigMonitor.postman_collection.json`, `assets/grafana/rigmonitor-integrations.json`, `rigmonitor-overview.json` | DCGM-only. |
| Tests | `src/Test.Xunit/NvidiaDcgmGpuTelemetryProviderTests.cs`, `TelemetryServiceCollectionStatusTests.cs`, `PersistenceTests.cs`, `src/Test.Shared/TestTelemetryComponents.cs` | NVIDIA fixtures only. The dashboard has no tests. |

## 3. Goals and non-goals

### Goals

1. Full telemetry for AMD Instinct, Radeon, and APU GPUs on Linux: utilization, VRAM/GTT, temperatures, power, clocks, ECC, partitions.
2. Full telemetry for Apple Silicon GPUs on macOS, **without root**: utilization (device, renderer, tiler), unified memory in use, recommended working set, power, active frequency, temperature, thermal state.
3. Telemetry for Qualcomm Adreno GPUs on Windows on Snapdragon (X Elite / X Plus / X2) and on Linux (`drm/msm`): utilization, shared/dedicated memory, frequency, and temperature where the platform exposes it.
4. Separate vendor sections in the API and dashboard, plus a vendor-neutral aggregate.
5. Hosts with GPUs from more than one vendor work correctly, including charts and rollups.
6. Existing NVIDIA history stays readable and is tagged with its vendor.
7. Native, in-process collection with no subprocesses or privilege escalation for AMD, Apple, and Qualcomm.
8. Every mapping from native data to RigMonitor models is testable on any OS through fakes and captured fixtures.

### Non-goals

- Vendor-specific Windows libraries for AMD (ADLX) and Intel (IGCL / Level Zero). Basic AMD and Intel telemetry on Windows comes from the vendor-neutral Windows backend in section 4.4. The vendor enum reserves `intel`.
- Qualcomm Hexagon NPU and Cloud AI 100 accelerators (follow-up, section 13). This plan covers the Adreno GPU only.
- Android (not a RigMonitor host platform).
- Per-process GPU accounting.
- Controlling GPU state (clocks, power caps, partition modes).
- Replacing DCGM with NVML for NVIDIA (follow-up, section 13).

## 4. Data sources

### 4.1 AMD (Linux)

Backend selection is `Telemetry.AmdSource`: `auto` (default), `native`, `sysfs`, `exporter`.

| Backend | `source` | Mechanism | Role in `auto` |
|---------|----------|-----------|----------------|
| ROCm AMD SMI library | `amdSmiLibrary` | P/Invoke into `libamd_smi.so` (`/opt/rocm/lib`, `LD_LIBRARY_PATH`, or `Telemetry.AmdSmiLibraryPath`) | **Primary.** Richest data: activity, VRAM, temperatures, power, clocks, ECC, partitions, UUID, BDF, market name. |
| `amdgpu` sysfs | `amdSysfs` | Direct file reads under `/sys/class/drm/card*/device` and its `hwmon` | **Gap-fill and fallback.** Adds GTT and fields the library returns as unsupported. Used for everything when ROCm is absent (consumer Radeon, APUs). |
| AMD Device Metrics Exporter | `amdMetricsExporter` | HTTP Prometheus text (default `http://localhost:5000/metrics`), with the parser shared with DCGM | **Last resort, or when chosen explicitly.** Used in containers without `/dev/kfd` access, or for remote exporters. |

**Why this order is the robust one:**

- The library is a stable C ABI that AMD versions (`amdsmi_get_lib_version`). It is far more stable than the CLI's JSON output, which changes between ROCm releases.
- sysfs comes from the kernel driver itself. It needs no ROCm and works on every `amdgpu` device.
- Merging the two **by PCI BDF** means a field the library can't provide for a given ASIC is still filled when the kernel exposes it, and devices are never counted twice.
- The exporter needs a separately installed service. It is kept for container and remote cases.

**Native library calls** (`AmdSmiNative.cs`, `[LibraryImport]`, loaded through `NativeLibrary.TryLoad`):

| Purpose | Call |
|---------|------|
| lifecycle | `amdsmi_init(AMDSMI_INIT_AMD_GPUS)` once. `amdsmi_shut_down` on dispose. |
| version gate | `amdsmi_get_lib_version`. Supported ABI range is declared in code. Outside it, log once and fall back to sysfs. |
| enumeration | `amdsmi_get_socket_handles` → `amdsmi_get_processor_handles` |
| identity | `amdsmi_get_gpu_asic_info` (market name, vendor/device id), `amdsmi_get_gpu_device_uuid`, `amdsmi_get_gpu_device_bdf`, `amdsmi_get_gpu_driver_info`, `amdsmi_get_gpu_compute_partition`, `amdsmi_get_gpu_memory_partition` (cached at warmup) |
| activity | `amdsmi_get_gpu_activity` (gfx, umc, mm) |
| memory | `amdsmi_get_gpu_vram_usage` (`vram_total`, `vram_used`) |
| temperatures | `amdsmi_get_temp_metric` for `EDGE`, `HOTSPOT`, `VRAM` with `AMDSMI_TEMP_CURRENT` |
| power | `amdsmi_get_power_info` (current socket power, falling back to average) |
| clocks | `amdsmi_get_clock_info` for `GFX` and `MEM` |
| errors | `amdsmi_get_gpu_total_ecc_count` |

Every call's status code is checked. `AMDSMI_STATUS_NOT_SUPPORTED` makes that field `null`, so sysfs can fill it. Any other error marks that device's metric unavailable for that sample and records a provider error. Struct layouts follow the library's ABI-stable headers (with their reserved padding). Each struct has a size assertion test.

**sysfs mapping** (root configurable through `Telemetry.AmdSysfsRoot` for tests):

| Field | Path under `cardN/device/` | Conversion |
|-------|----------------------------|------------|
| filter | `vendor` == `0x1002` | Skip connector nodes (`cardN-DP-1`) and non-AMD vendors. |
| utilization | `gpu_busy_percent` | |
| memory activity | `mem_busy_percent` | |
| VRAM used/total | `mem_info_vram_used`, `mem_info_vram_total` | bytes to MB |
| GTT used/total | `mem_info_gtt_used`, `mem_info_gtt_total` | bytes to MB |
| temperatures | `hwmon/hwmon*/temp*_input` matched by `temp*_label` (`edge`, `junction`, `mem`) | millidegrees to C |
| power | `hwmon/hwmon*/power1_average`, falling back to `power1_input` | microwatts to W |
| clocks | `hwmon/hwmon*/freq1_input` (sclk), `freq2_input` (mclk) | Hz to MHz |
| identity | `unique_id`, `uevent` (`PCI_SLOT_NAME`, `PCI_ID`), `product_name` when present | |

**APU / unified memory:** a device is `unifiedMemory` when the ASIC info reports an APU, or VRAM total is no more than 1/8 of GTT total, or the PCI ID is on a known-APU list (MI300A, Ryzen AI Max / Strix Halo, Phoenix, Hawk Point). For these devices, "available GPU memory" for placement is VRAM carve-out + GTT, which the AMD section reports separately.

**Permissions:** native and sysfs reads need the service user in the `render` and `video` groups (and access to `/dev/kfd` for the library). The systemd unit sets `SupplementaryGroups=render video`. RigMonitor never runs as root.

### 4.2 Apple (macOS)

Everything runs in-process. Code lives in `src/RigMonitor.Telemetry/Platform/Mac/Native/`.

| Signal | Framework / API | Public? | Notes |
|--------|-----------------|---------|-------|
| Accelerator discovery and identity | IOKit: `IOServiceGetMatchingServices(kIOMainPortDefault, IOServiceMatching("IOAccelerator"))`, `IORegistryEntryCreateCFProperties` → keys `model`, `gpu-core-count`, `IOClass` | Public | Covers Apple Silicon (`AGXAccelerator*`) and Intel Macs (`AMDRadeon*`, `Intel*`). |
| Utilization | Same registry entry, `PerformanceStatistics` dictionary: `Device Utilization %`, `Renderer Utilization %`, `Tiler Utilization %` | Public API, undocumented keys | The same values Activity Monitor's GPU History shows. |
| GPU memory in use | `PerformanceStatistics`: `In use system memory`, `Alloc system memory` | Public API, undocumented keys | |
| Chip and GPU capabilities | Metal through the ObjC runtime (`libobjc` `objc_msgSend`): `MTLCreateSystemDefaultDevice`, `name`, `hasUnifiedMemory`, `recommendedMaxWorkingSetSize`, `supportsFamily:` (Metal family) | Public | Cached, re-read hourly. |
| GPU wired memory limit | `sysctlbyname("iogpu.wired_limit_mb")`, `sysctlbyname("hw.memsize")` via libc | Public | `0` means the OS default. |
| Thermal state | Foundation `NSProcessInfo.processInfo.thermalState` | Public | `nominal`, `fair`, `serious`, `critical`. |
| GPU power | IOReport (`/usr/lib/libIOReport.dylib`): subscribe to group `Energy Model`, channel `GPU Energy`. Watts = energy delta / time delta between samples. | Undocumented | No root required. |
| Active frequency and residency | IOReport group `GPU Stats`, subgroup `GPU Performance States`: residency per P-state. Frequencies from the `pmgr` IORegistry entry's voltage-states table. | Undocumented | Frequency table key differs by chip family (M1–M3 vs M4+). Handled by a mapping table. |
| GPU temperature | IOHIDFamily `IOHIDEventSystemClientCreate`, matched on usage page `0xff00` / usage `5`, `IOHIDServiceClientCopyEvent(kIOHIDEventTypeTemperature)`. Sensors chosen by `Product` name (GPU die sensors). Report the average and max. | Undocumented | Sensor names differ by chip. Handled by a mapping table plus a generic `GPU` name match. |

**How the undocumented APIs are made safe:**

1. **Loaded at runtime** with `NativeLibrary.TryLoad` / `TryGetExport`. A missing library or symbol sets a per-feature flag, logs one warning, and leaves the field `null`. The process never fails to start because of it.
2. **Isolated** behind `IAppleGpuNative`, which returns plain DTOs. All parsing, mapping, delta maths, and chip-specific tables live in managed code that is tested on any OS with fakes.
3. **CoreFoundation ownership** follows the Create/Copy rule. Every `CFTypeRef` sits in a `SafeHandle` (`CFSafeHandle`) that calls `CFRelease`, so nothing leaks over long runs. A soak test checks this (section 8.3).
4. **Stateful samplers** (IOReport energy/residency deltas) run in a background sampler, using the same pattern as `DefaultNetworkRateSampler`, so `/v1/telemetry` never blocks waiting for an interval.
5. **Per-feature kill switches** in settings let an operator turn off one source if a future macOS release misbehaves, without losing the rest.
6. **macOS CI** on Apple Silicon runs the real native layer on every PR, so an OS image update that breaks a key fails the build instead of reaching users.

**Intel Macs:** IOKit and Metal work. IOReport and IOHID GPU channels usually don't exist there, so power, frequency, and temperature are `null`. Best effort, documented.

**Docker Desktop on macOS** runs a Linux VM with no GPU access. The Apple section is `unsupported` inside containers. A native run is required, and the docs say so.

### 4.3 NVIDIA

No change in mechanism. The DCGM exporter is still read through the shared Prometheus parser. The provider now fills the `nvidia` section model (section 5.3) and optionally reads `DCGM_FI_DEV_MEMORY_TEMP`, `DCGM_FI_DEV_MEM_COPY_UTIL`, and `DCGM_FI_DEV_ECC_DBE_VOL_TOTAL` when present.

### 4.4 Qualcomm Adreno (Windows on Snapdragon, Linux `drm/msm`)

Qualcomm GPUs reach RigMonitor hosts mainly through Snapdragon X-series Windows laptops and dev kits (Adreno X1/X2), and through Arm Linux boards and laptops running the upstream `msm` DRM driver. Qualcomm publishes no NVML-style management library for these GPUs. The OS graphics stack is the supported source of truth, and it is what Task Manager and the Linux desktop tools read. Code lives in `src/RigMonitor.Telemetry/Gpu/Qualcomm/` and `src/RigMonitor.Telemetry/Platform/Windows/Native/`.

Backend selection is `Telemetry.QualcommSource`: `auto` (default), `windows`, `drmMsm`.

#### Windows on Snapdragon: `source = windowsGraphicsKernel`

| Signal | Native API | Notes |
|--------|------------|-------|
| Adapter discovery and identity | DXGI `CreateDXGIFactory1` → `EnumAdapters1` → `DXGI_ADAPTER_DESC1` (`VendorId` `0x5143` = Qualcomm, `DeviceId`, `Description`, `AdapterLuid`, `DedicatedVideoMemory`, `SharedSystemMemory`). Skip `DXGI_ADAPTER_FLAG_SOFTWARE` (Basic Render Driver). | P/Invoke to `dxgi.dll` through COM vtable calls (`[GeneratedComInterface]`). |
| Driver version | `IDXGIAdapter::CheckInterfaceSupport` (UMD version) or registry `DriverVersion` under the adapter's device key | |
| Memory usage and budget | `IDXGIAdapter3::QueryVideoMemoryInfo` for `LOCAL` and `NON_LOCAL` segment groups (`CurrentUsage`, `Budget`). Adreno is unified-memory, so most allocations are in the shared segment. | `unifiedMemory = true`. Budget is the placement-relevant number, like Apple's working set. |
| Utilization per engine | PDH counters `\GPU Engine(*)\Utilization Percentage`, filtered by the adapter LUID in the instance name (`luid_0x..._phys_0_eng_N_engtype_3D/Compute/Copy/VideoDecode`). Utilization = max over the 3D and Compute engines, which matches Task Manager. Per-engine values go in the Qualcomm section. | `pdh.dll` P/Invoke. Rate counters need two collections, so a background sampler is used (the same pattern as Apple). |
| Memory by process total | PDH `\GPU Adapter Memory(*)\Dedicated Usage` and `Shared Usage` | Cross-check against DXGI. |
| Frequency | D3DKMT `D3DKMTQueryAdapterInfo(KMTQAITYPE_NODEMETADATA / ADAPTERPERFDATA)`: `CurrentFrequency`, `MaxFrequency`, `Power`, `Temperature` fields of `D3DKMT_ADAPTER_PERFDATA` | `gdi32.dll` P/Invoke. These are populated only when the driver implements WDDM 2.4+ perf data. Every field is nullable and verified per driver version. |
| Temperature / power | `D3DKMT_ADAPTER_PERFDATA` when the driver fills it. Otherwise `null`. | No WMI thermal zones (needs admin, not GPU-specific). |

**Vendor-neutral by design.** The Windows backend keys only on DXGI vendor IDs. Qualcomm (`0x5143`) devices go to the `qualcomm` section. Other IDs (AMD `0x1002`, Intel `0x8086`) can feed the `amd` section and a future `intel` section with the same basic telemetry. That turns the earlier "no AMD on Windows" non-goal into basic support. Mapping AMD-on-Windows into the `amd` section is gated behind `Telemetry.WindowsGenericGpuEnabled` (default `true`). NVIDIA adapters are skipped when DCGM is available, to avoid counting them twice.

#### Linux `drm/msm`: `source = drmMsm`

| Signal | Source | Notes |
|--------|--------|-------|
| Discovery | `/sys/class/drm/card*/device/driver` → `msm` / `msm_dpu` (the GPU node is the one with a render node `renderD*`), `of_node/compatible` (for example `qcom,adreno-43050c01`) for the model | Device-tree based, not PCI. `busId` is the platform device name. |
| Frequency and load | devfreq `/sys/class/devfreq/*.gpu/cur_freq`, `max_freq`, `min_freq`, and `trans_stat` (time per frequency) | `trans_stat` deltas give busy residency when the governor reports it. |
| Utilization | DRM fdinfo: sum `drm-engine-gpu` ns deltas across `/proc/*/fdinfo/*` entries for the msm render node, divided by wall time | Without elevated rights, only processes owned by the RigMonitor user are visible. `utilizationScope` is reported as `allProcesses` or `ownProcesses` so the number is never silently wrong. If devfreq busy time is available, it is preferred and the scope is `device`. |
| Memory | fdinfo `drm-total-memory` / `drm-resident-memory` summed per device, plus host RAM as total (unified) | `unifiedMemory = true`. |
| Temperature | `/sys/class/thermal/thermal_zone*/type` matching `gpu*` (for example `gpuss-0-thermal`, `gpu-top-thermal`), with `temp` in millidegrees | Average and max across matching zones. |
| Power | `null` (no stable upstream interface) | |
| Driver | kernel release + `/sys/module/msm/version` if present | |

Downstream Android-style `kgsl` kernels (`/sys/class/kgsl/kgsl-3d0/gpu_busy_percentage`, `gpuclk`, `temp`) are detected too, with `source = kgsl`. These cover boards that ship vendor BSP kernels.

**Permissions:** sysfs, devfreq, and thermal reads need no special rights. fdinfo for other users' processes requires `CAP_SYS_PTRACE`, which RigMonitor does **not** request. The utilization scope field and the devfreq preference above handle this honestly. On Windows, PDH and DXGI work as a standard user.

**Maturity:** Qualcomm interfaces vary more by driver release than AMD or Apple do. Each field is optional and verified against fixtures captured from real devices. The Qualcomm section shows a per-field source map in its detail row (like AMD's), so gaps are explained rather than silent.

## 5. Backend design

### 5.1 Project layout

```
src/RigMonitor.Core/
  Enums/GpuVendorEnum.cs                  NEW  Unknown, Nvidia, Amd, Apple, Qualcomm, Intel, Mixed
  Enums/GpuTelemetrySourceEnum.cs         NEW  DcgmExporter, AmdSmiLibrary, AmdSysfs,
                                               AmdMetricsExporter, AppleNative,
                                               WindowsGraphicsKernel, DrmMsm, Kgsl
  Enums/GpuUtilizationScopeEnum.cs        NEW  Device, AllProcesses, OwnProcesses
  Enums/GpuThermalStateEnum.cs            NEW  Nominal, Fair, Serious, Critical
  Models/Gpu/GpuDeviceTelemetry.cs        CHG  common base (moved)
  Models/Gpu/GpuUtilizationTelemetry.cs   CHG  common metrics, optional ones nullable
  Models/Gpu/GpuTelemetry.cs              CHG  aggregate section
  Models/Gpu/NvidiaGpuTelemetry.cs        NEW  + NvidiaGpuDeviceTelemetry
  Models/Gpu/AmdGpuTelemetry.cs           NEW  + AmdGpuDeviceTelemetry
  Models/Gpu/AppleGpuTelemetry.cs         NEW  + AppleGpuDeviceTelemetry
  Models/Gpu/QualcommGpuTelemetry.cs      NEW  + QualcommGpuDeviceTelemetry
  Models/Gpu/GpuVendorRollup.cs           NEW  per-vendor rollup bucket values
  Models/TelemetrySnapshot.cs             CHG  + Nvidia, Amd, Apple, Qualcomm
  Models/TelemetryCollectionMetadata.cs   CHG  + Nvidia, Amd, Apple, Qualcomm
  Models/TelemetryRequestOptions.cs       CHG  + IncludeNvidia, IncludeAmd, IncludeApple, IncludeQualcomm
  Models/RuntimeCapabilities.cs           CHG  + AmdAvailable, AppleGpuAvailable, QualcommAvailable,
                                               GpuAvailable, AmdSource, QualcommSource,
                                               AmdMetricsExporterUrl, AppleFeatures
  Services/Interfaces/INvidiaGpuTelemetryProvider.cs   NEW
  Services/Interfaces/IAmdGpuTelemetryProvider.cs      NEW
  Services/Interfaces/IAppleGpuTelemetryProvider.cs    NEW
  Services/Interfaces/IQualcommGpuTelemetryProvider.cs NEW
  Services/Interfaces/IPrometheusTextClient.cs        NEW  (IDcgmExporterClient becomes [Obsolete] shim)
  Settings/TelemetrySettings.cs           CHG  section 5.5

src/RigMonitor.Telemetry/
  Gpu/Prometheus/PrometheusTextParser.cs  NEW  extracted from the DCGM provider
  Gpu/Prometheus/PrometheusTextClient.cs  NEW  generalized DcgmExporterClient
  Gpu/GpuAggregator.cs                    NEW  builds the `gpu` section from the vendor sections
  Gpu/Nvidia/NvidiaDcgmGpuTelemetryProvider.cs   MOVE
  Gpu/Amd/AmdGpuTelemetryProvider.cs      NEW  backend selection + BDF merge
  Gpu/Amd/AmdSmiLibraryBackend.cs         NEW
  Gpu/Amd/Native/AmdSmiNative.cs          NEW  [LibraryImport] + structs
  Gpu/Amd/Native/IAmdSmiNative.cs         NEW  seam for tests
  Gpu/Amd/AmdSysfsBackend.cs              NEW
  Gpu/Amd/AmdMetricsExporterBackend.cs    NEW
  Platform/Mac/Native/CoreFoundation.cs   NEW  CF interop + CFSafeHandle + CF→.NET converters
  Platform/Mac/Native/IOKit.cs            NEW
  Platform/Mac/Native/ObjCRuntime.cs      NEW  objc_getClass, sel_registerName, objc_msgSend variants
  Platform/Mac/Native/Metal.cs            NEW
  Platform/Mac/Native/IOReport.cs         NEW  runtime-loaded
  Platform/Mac/Native/IOHidSensors.cs     NEW  runtime-loaded
  Platform/Mac/Native/Sysctl.cs           NEW
  Platform/Mac/Native/IAppleGpuNative.cs  NEW  seam returning DTOs
  Platform/Mac/Native/AppleGpuNative.cs   NEW  real implementation
  Gpu/Apple/AppleGpuTelemetryProvider.cs  NEW  maps DTOs → models
  Gpu/Apple/AppleGpuSampler.cs            NEW  background IOReport delta sampler
  Gpu/Apple/AppleChipTables.cs            NEW  P-state frequency keys, sensor names by chip family
  Platform/Windows/Native/Dxgi.cs         NEW  [GeneratedComInterface] IDXGIFactory1/IDXGIAdapter3
  Platform/Windows/Native/Pdh.cs          NEW  PdhOpenQuery/AddEnglishCounter/CollectQueryData
  Platform/Windows/Native/D3dkmt.cs       NEW  D3DKMTOpenAdapterFromLuid/QueryAdapterInfo
  Platform/Windows/Native/IWindowsGpuNative.cs NEW  seam returning DTOs
  Gpu/Windows/WindowsGraphicsKernelBackend.cs  NEW  vendor-neutral; routes adapters by VendorId
  Gpu/Windows/WindowsGpuSampler.cs        NEW  background PDH rate sampler
  Gpu/Qualcomm/QualcommGpuTelemetryProvider.cs NEW  backend selection
  Gpu/Qualcomm/DrmMsmBackend.cs           NEW  devfreq + fdinfo + thermal (root-path injectable)
  Gpu/Qualcomm/KgslBackend.cs             NEW  downstream kgsl sysfs
  Gpu/Qualcomm/AdrenoModelTable.cs        NEW  DXGI DeviceId / DT compatible → marketing name
  Services/RuntimeCapabilitiesService.cs  CHG
  Services/TelemetryService.cs            CHG
```

`RigMonitor.Telemetry.csproj` gets `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (needed for `[LibraryImport]` with struct pointers). No new NuGet packages.

### 5.2 Sections and selectors

New snapshot sections and selector keys: `nvidia`, `amd`, `apple`, `qualcomm`. The existing `gpu` key stays as the aggregate.

| Request | Collected internally | Emitted |
|---------|----------------------|---------|
| no selectors | all vendors | `nvidia`, `amd`, `apple`, `qualcomm`, `gpu` |
| `?gpu` | all vendors | `gpu` only |
| `?amd` | AMD | `amd` only |
| `?gpu&apple` | all vendors | `gpu`, `apple` |
| `?nvidia=false` (with other keys) | normal selector rules | `nvidia` omitted, `collection.nvidia.statusCode = disabled` |

Each vendor section is collected once per snapshot through the existing `CollectSectionAsync`, so it gets `requested`, `supported`, `statusCode`, `freshness`, `lastSuccessUtc`, and the stale-window behaviour natively. Vendor sections run **in parallel**.

`collection.gpu` is derived:

- `supported` = any vendor supported
- `statusCode` = `ok` if at least one requested+supported vendor is `ok`. Otherwise the most severe vendor status, in the order `error` > `stale` > `unavailable` > `unsupported`.
- `message` names the vendors that are not ok (for example `"amd: unavailable"`).

`IsFullScope` and `DetermineSnapshotOutcome` in `TelemetryService` include the four new sections.

### 5.3 Models

**Common base** (`GpuDeviceTelemetry`, used in the `gpu` aggregate and inherited by vendor devices):
`ordinal` (global 0..n-1), `deviceIndex` (vendor-native), `vendor`, `source`, `uuid`, `busId`, `model`, `driverVersion`, `unifiedMemory`, plus `metrics`:

| Common metric | Type | NVIDIA | AMD | Apple |
|---------------|------|--------|-----|-------|
| `gpuUtilizationPercent` | `double` | yes | yes | yes (device util) |
| `memoryUsed/Free/TotalMegabytes`, `memoryUtilizationPercent` | `double` | framebuffer | VRAM (+GTT on APU, see AMD section) | unified (in use / host RAM) |
| `memorySource`, `memoryShared` | as today | | | |
| `temperatureCelsius` | `double?` | yes | edge, else hotspot | GPU die average |
| `powerUsageWatts` | `double?` | yes | yes | IOReport |
| `smClockMHz` | `double?` (documented as primary compute clock) | SM | sclk | active frequency |
| `memoryClockMHz` | `double?` | yes | yes | null |

The vendor-specific `xidErrors` and `migProfile` **move** to the NVIDIA section (approved breaking change).

**`nvidia` section** (`NvidiaGpuTelemetry`): `source`, `endpoint`, `devices[]` of `NvidiaGpuDeviceTelemetry` = base + `migProfile`, `xidErrors`, `memoryTemperatureCelsius?`, `memoryCopyUtilizationPercent?`, `uncorrectableEccErrors?`.

**`amd` section** (`AmdGpuTelemetry`): `source` (resolved backend), `gapFillSource` (`amdSysfs` or null), `libraryVersion`, `endpoint`, `devices[]` of `AmdGpuDeviceTelemetry` = base + `hotspotTemperatureCelsius?`, `memoryTemperatureCelsius?`, `memoryActivityPercent?`, `multimediaActivityPercent?`, `gttUsedMegabytes?`, `gttTotalMegabytes?`, `uncorrectableEccErrors?`, `correctableEccErrors?`, `partitionId?`, `computePartition?`, `memoryPartition?`, `fieldSources` (map of field → `amdSmiLibrary`/`amdSysfs`, shown in JSON for diagnosis).

**`apple` section** (`AppleGpuTelemetry`): `source = appleNative`, `chip` (for example "Apple M3 Max"), `gpuCoreCount`, `metalFamily`, `thermalState`, `hostMemoryTotalMegabytes`, `recommendedMaxWorkingSetMegabytes`, `wiredLimitMegabytes?`, `features` (which native sources are active: `ioKit`, `metal`, `ioReport`, `hidSensors`), and `devices[]` of `AppleGpuDeviceTelemetry` = base + `rendererUtilizationPercent?`, `tilerUtilizationPercent?`, `allocatedSystemMemoryMegabytes?`, `activeResidencyPercent?`, `maxTemperatureCelsius?`.

**`qualcomm` section** (`QualcommGpuTelemetry`): `source` (`windowsGraphicsKernel` / `drmMsm` / `kgsl`), `driverVersion`, and `devices[]` of `QualcommGpuDeviceTelemetry` = base + `adapterLuid?` (Windows), `dxgiDeviceId?`, `compatible?` (Linux device tree), `utilizationScope` (`device` / `allProcesses` / `ownProcesses`), `engineUtilization` (map: `3d`, `compute`, `copy`, `videoDecode`, `videoEncode` → percent), `sharedMemoryUsedMegabytes?`, `sharedMemoryBudgetMegabytes?`, `dedicatedMemoryUsedMegabytes?`, `dedicatedMemoryBudgetMegabytes?`, `currentFrequencyMHz?`, `maxFrequencyMHz?`, `maxTemperatureCelsius?`, `fieldSources`.

On the common metrics, Qualcomm devices set `unifiedMemory = true`, `memoryUsedMegabytes` = shared + dedicated usage, `memoryTotalMegabytes` = shared + dedicated budget (Windows) or host RAM (Linux), and `smClockMHz` = current frequency.

**Aggregate `gpu`**: `vendor` (`nvidia`/`amd`/`apple`/`qualcomm`/`mixed`), `vendors[]` (those present), `devices[]` (common base shape only, sorted by vendor then native index, ordinal assigned). `exporterEndpoint` is removed. Endpoints live in the vendor sections.

**Unified-memory fallback** (`TelemetryService.ApplyUnifiedMemoryGpuFallback`): driven by `device.UnifiedMemory` instead of model strings. NVIDIA sets it for GB10 / DGX Spark (same match, moved into the provider). AMD sets it for APUs. Apple always sets it. For Apple, `memoryUsedMegabytes` stays the GPU's in-use memory and `memoryTotalMegabytes` = host RAM, so memory utilization means "share of unified memory the GPU holds".

**Capabilities** additions: `gpuAvailable`, `amdAvailable`, `appleGpuAvailable`, `qualcommAvailable`, `amdSource`, `qualcommSource`, `amdMetricsExporterUrl`, `appleFeatures` (`ioKit`, `metal`, `ioReport`, `hidSensors` booleans). `nvidiaAvailable` and `dcgmExporterUrl` stay.

Serialization: enums are camelCase through the existing `JsonStringEnumConverter`. Nulls are omitted through the existing `WhenWritingNull`. A tolerant converter reads the legacy `"vendor": "NVIDIA"` and legacy device fields (`metrics.xidErrors`, `migProfile`) in stored `snapshotjson`.

### 5.4 Capability probing

- Platform gating: Linux → NVIDIA + AMD + Qualcomm (Qualcomm only on Arm64, or when an `msm`/`kgsl` node exists); Windows → NVIDIA + the vendor-neutral Windows backend (Qualcomm, and AMD/Intel basic); macOS → Apple (+ NVIDIA only when `DcgmExporterUrl` is non-default, for remote exporters).
- Probes run in parallel at startup, each bounded by `RequestTimeoutMs`.
- AMD `auto`: try `libamd_smi` init. If it finds at least one GPU, that is primary, with sysfs gap-fill. Else try sysfs. Else try the exporter URL. The resolved backend is reported in `capabilities.amdSource`.
- Apple: capability = IOKit finds at least one `IOAccelerator`. Feature flags record which optional native sources loaded.
- Qualcomm: Windows capability = DXGI enumerates a hardware adapter with `VendorId 0x5143`. Linux capability = a DRM card bound to `msm` with a render node, or `/sys/class/kgsl/kgsl-3d0` exists.
- Startup log lines: `AMD available: true (source: amdSmiLibrary, gap-fill: amdSysfs, lib 25.x)`, `Apple GPU available: true (features: ioKit, metal, ioReport, hidSensors)`, `Qualcomm GPU available: true (source: windowsGraphicsKernel, Adreno X1-85)`.

### 5.5 Settings (`Telemetry.*`)

| Setting | Default | Values / clamp |
|---------|---------|----------------|
| `NvidiaEnabled` | `true` | bool |
| `DcgmExporterUrl` | unchanged | |
| `AmdEnabled` | `true` (Linux only) | bool |
| `AmdSource` | `auto` | `auto`, `native`, `sysfs`, `exporter`. Invalid values fall back to `auto` with a warning. |
| `AmdSmiLibraryPath` | empty (search `/opt/rocm/lib`, `/opt/rocm-*/lib`, loader path) | |
| `AmdSysfsGapFillEnabled` | `true` | bool |
| `AmdSysfsRoot` | `/sys/class/drm` | |
| `AmdMetricsExporterUrl` | `http://localhost:5000/metrics` | |
| `AppleGpuEnabled` | `true` (macOS only) | bool |
| `AppleIOReportEnabled` | `true` | bool (kill switch) |
| `AppleHidSensorsEnabled` | `true` | bool (kill switch) |
| `AppleSamplerIntervalMs` | `1000` | 250 to 10000 |
| `QualcommEnabled` | `true` (Windows Arm64 and Linux) | bool |
| `QualcommSource` | `auto` | `auto`, `windows`, `drmMsm`, `kgsl`. Invalid values fall back to `auto`. |
| `QualcommSysfsRoot` / `QualcommProcRoot` | `/sys` / `/proc` | For tests. |
| `WindowsGenericGpuEnabled` | `true` | Feed AMD/Intel adapters seen by the Windows backend into their vendor sections. |
| `WindowsGpuSamplerIntervalMs` | `1000` | 250 to 10000 |

`SettingsManager` rewrites `rigmonitor.json` with new defaults automatically.

### 5.6 Native-code engineering standards

- `[LibraryImport]` (source-generated) everywhere, with blittable structs and `[StructLayout(LayoutKind.Sequential)]`. A size/offset unit test per struct.
- One `IntegrationCall` span per native sampling call (`amdSmiLibrary sample`, `apple ioKit`, `apple ioReport`, `apple hid`), so native work shows in traces like HTTP integrations do.
- All native handles in `SafeHandle` subclasses. Dispose order: AMD shutdown on server stop, IOReport subscription release, HID client release.
- Thread safety: the AMD library is called under a single `SemaphoreSlim`. The Apple sampler owns its IOReport subscription on one thread.
- No `unsafe` code outside `Native/` folders.
- Cross-platform builds compile all interop. Runtime use sits behind `OperatingSystem.IsMacOS()` / `IsLinux()` and `[SupportedOSPlatform]` attributes, and the platform analyzer runs with warnings as errors for these folders.

### 5.7 Persistence (SQLite)

**Schema** (in fresh `CREATE TABLE`, and as migrations in `GetMigrationStatements()`, which already ignore duplicate-column errors):

```sql
ALTER TABLE telemetry_samples ADD COLUMN amdavailable INTEGER NOT NULL DEFAULT 0;
ALTER TABLE telemetry_samples ADD COLUMN applegpuavailable INTEGER NOT NULL DEFAULT 0;
ALTER TABLE telemetry_samples ADD COLUMN qualcommavailable INTEGER NOT NULL DEFAULT 0;
ALTER TABLE telemetry_samples ADD COLUMN gpuvendor TEXT;                 -- nvidia|amd|apple|qualcomm|mixed
ALTER TABLE telemetry_gpu_samples ADD COLUMN vendor TEXT;
ALTER TABLE telemetry_gpu_samples ADD COLUMN source TEXT;
ALTER TABLE telemetry_gpu_samples ADD COLUMN ordinal INTEGER;
ALTER TABLE telemetry_gpu_samples ADD COLUMN unifiedmemory INTEGER;
ALTER TABLE telemetry_gpu_samples ADD COLUMN hotspottemperaturecelsius REAL;   -- AMD
ALTER TABLE telemetry_gpu_samples ADD COLUMN memorytemperaturecelsius REAL;    -- AMD/NVIDIA
ALTER TABLE telemetry_gpu_samples ADD COLUMN gttusedmegabytes REAL;            -- AMD
ALTER TABLE telemetry_gpu_samples ADD COLUMN gtttotalmegabytes REAL;           -- AMD
ALTER TABLE telemetry_gpu_samples ADD COLUMN uncorrectableeccerrors INTEGER;   -- AMD/NVIDIA
ALTER TABLE telemetry_gpu_samples ADD COLUMN partitionid TEXT;                 -- AMD
ALTER TABLE telemetry_gpu_samples ADD COLUMN thermalstate INTEGER;             -- Apple (0..3)
ALTER TABLE telemetry_gpu_samples ADD COLUMN workingsetmegabytes REAL;         -- Apple
ALTER TABLE telemetry_gpu_samples ADD COLUMN memorybudgetmegabytes REAL;       -- Qualcomm (DXGI budget)
ALTER TABLE telemetry_gpu_samples ADD COLUMN utilizationscope TEXT;            -- Qualcomm/Linux
ALTER TABLE telemetry_gpu_samples ADD COLUMN vendordetailjson TEXT;            -- remaining vendor fields
CREATE INDEX IF NOT EXISTS idx_telemetry_gpu_samples_vendor_collectedutc ON telemetry_gpu_samples(vendor, collectedutc);
CREATE INDEX IF NOT EXISTS idx_telemetry_gpu_samples_sampleid_vendor ON telemetry_gpu_samples(sampleid, vendor);
-- Backfill: every pre-existing GPU row came from DCGM.
UPDATE telemetry_gpu_samples SET vendor = 'nvidia', source = 'dcgmExporter' WHERE vendor IS NULL;
UPDATE telemetry_samples SET gpuvendor = 'nvidia' WHERE gpuvendor IS NULL AND gpudevicecount > 0;
```

Columns used by filters or charts are real columns. Everything else vendor-specific goes in `vendordetailjson`, which stays out of query paths.

**Write path:**

- One `telemetry_gpu_samples` row per device across all vendors, from the aggregate plus each device's vendor-specific fields.
- `ApplyGpuSummary`: average temperature and power over non-null values only. Store `NULL` when no device reports the value, so Apple rows on Intel Macs never pull fleet averages to zero.

**Search** (`TelemetryHistorySearchFilter`): add `GpuVendor`, `AmdAvailable`, `AppleGpuAvailable`, `QualcommAvailable`. The vendor filter is an `EXISTS` subquery on `telemetry_gpu_samples.vendor`, following the `GpuUuid` pattern.

**Rollups:**

- `TelemetryRollupRequest` gains `GpuVendor` (filter) and `IncludeGpuVendorBreakdown` (default `true`).
- `TelemetryRollupBucket` gains `gpuVendors: { nvidia?: GpuVendorRollup, amd?: GpuVendorRollup, apple?: GpuVendorRollup, qualcomm?: GpuVendorRollup }`.
- `GpuVendorRollup`: `averageDeviceCount`, `average/min/maxUtilizationPercent`, `averageMemoryUtilizationPercent`, `averageMemoryUsedMegabytes`, `averageMemoryTotalMegabytes`, `average/min/maxTemperatureCelsius`, `averagePowerUsageWatts`. Vendor-specific: `averageHotspotTemperatureCelsius` (AMD), `averageGttUsedMegabytes` (AMD), `averageWorkingSetMegabytes` (Apple), `maxThermalState` (Apple), `averageMemoryBudgetMegabytes` (Qualcomm).
- Computed in **one extra grouped query** per rollup request: first aggregate per (sample, vendor) — sum power and memory, average utilization and temperature — then aggregate per (bucket, vendor). This two-level aggregation is required so "power" means total vendor power per sample, averaged over the bucket, and not the per-device average.
- The existing host-level bucket fields stay (aggregate across vendors). With `GpuVendor` set, they are recomputed from that vendor's rows only.

**Detail endpoint:** `TelemetrySampleDetail` carries `amdAvailable`, `appleGpuAvailable`, `qualcommAvailable`, `gpuVendor`. Snapshot JSON from older versions deserializes through the legacy converter.

### 5.8 Observability (RigMonitor's own telemetry)

- New integration services in `RigMonitorTelemetryNames`: `amdSmiLibrary` (`init`, `sample`), `amdSysfs` (`read`), `amdMetricsExporter` (`metrics`), `appleNative` (`ioKit`, `metal`, `ioReport`, `hid`, `sysctl`), `windowsGraphics` (`dxgi`, `pdh`, `d3dkmt`), `qualcommLinux` (`devfreq`, `fdinfo`, `thermal`, `kgsl`).
- New sections in `rigmonitor.section` label values: `nvidia`, `amd`, `apple`, `qualcomm` (fixed set).
- Capability gauge label values: `amd`, `apple`, `qualcomm`. New gauge `rigmonitor.gpu.native.feature.available` with label `feature` (`amdSmiLibrary`, `amdSysfs`, `ioKit`, `metal`, `ioReport`, `hidSensors`, `dxgi`, `pdh`, `d3dkmtPerfData`, `drmMsm`, `kgsl`) to monitor degraded native features across a fleet.
- Counter `rigmonitor.gpu.native.errors` with labels `rigmonitor.gpu.vendor` and `feature`. No device ids on metrics, as TELEMETRY.md requires.
- Update `RigMonitorMetricCatalog` and the provisioned Grafana dashboards: integrations (a row per GPU source), overview (capability tiles per vendor), collection (vendor section status).

### 5.9 API surface

- `/v1/telemetry`: new `nvidia`, `amd`, `apple`, `qualcomm` sections and selectors. The `gpu` aggregate is reshaped as above.
- `/v1/capabilities`: new fields. OpenAPI description updated in `GeneralRoutes.cs`.
- History search/rollups: new filters and the `gpuVendors` bucket breakdown, in the OpenAPI schemas.

## 6. Frontend (dashboard)

### 6.1 Overview (`HomeView.jsx`)

**Header capability row:** one pill per vendor (`NVIDIA`, `AMD`, `Apple`, `Qualcomm`), green when available and muted when not. The hover title shows the source and endpoint/library version. The old single "GPU enabled" pill is removed.

**Stat cards row:**

- `GPU RAM` card: total across vendors, with the shared suffix when any device is unified.
- New `GPU utilization` card: device-weighted average across all GPUs, with a per-vendor breakdown in the tooltip.
- New `GPU power` card: sum across vendors that report power.

**Four vendor sections** replace the single GPU card. They always render in the order NVIDIA, AMD, Apple, Qualcomm, each a `SectionCard` bound to its own `collection.<vendor>` status. A vendor that isn't present shows the standard placeholder (for example "AMD GPU telemetry is unsupported on this host") with a link to that vendor's setup guide. A new dashboard setting, `Dashboard.HideUnsupportedGpuSections` (default `false`), lets operators hide sections for vendors their host can never have.

| Section | Header pills | Device table columns | Expandable detail row |
|---------|--------------|----------------------|-----------------------|
| **NVIDIA** | source `DCGM exporter`, endpoint | Model, VRAM, VRAM %, Utilization, Temp, Power, SM clock | UUID, bus id, driver, MIG profile, XID errors, memory clock, memory temp, ECC |
| **AMD** | source (`AMD SMI library` / `sysfs` / `exporter`), gap-fill badge, library version | Model (+ partition), VRAM, VRAM %, GTT (APUs), Utilization, Memory activity, Edge / Hotspot temp, Power | UUID, BDF, driver, compute/memory partition, ECC (correctable/uncorrectable), clocks, memory temp, per-field source |
| **Apple** | chip name, `N-core GPU`, Metal family, thermal state pill (success/success/warning/danger) | Device util, Renderer util, Tiler util, GPU memory in use, Power, Frequency, Temp | Allocated memory, active residency, max temp, wired limit, active native features |
| **Qualcomm** | source (`Windows graphics kernel` / `drm/msm` / `kgsl`), Adreno model, driver version, utilization-scope badge when not `device` | Model, Shared memory used / budget, Utilization, 3D, Compute, Frequency (current / max), Temp | Engine utilization breakdown (copy, video decode/encode), dedicated memory, adapter LUID or DT compatible, per-field source |

The Apple section also gets a **unified memory bar**: GPU in use / recommended working set / host total. This is the key number for model placement on Macs. The Qualcomm section reuses the same `UnifiedMemoryBar` (GPU in use / DXGI budget / host total).

All metric cells go through a new `formatOptional(value, unit)` helper that shows `—` for null. That removes today's `0 C` / `NaN C` rendering.

**Utilyze card:** unchanged, but the device label is prefixed with the vendor when the host is mixed.

### 6.2 Analytics (`TelemetryAnalyticsView.jsx`)

**Multi-series charts.** Extend the existing SVG line chart (`views/TelemetryAnalyticsView.jsx:347-427`) to accept `series[]`. Each series has its own polyline, a light area fill for single-series charts only, and a legend with toggles. Series colours come from CSS tokens with light and dark variants (`--series-nvidia`, `--series-amd`, `--series-apple`, `--series-qualcomm`, `--series-all`), checked for contrast in both themes.

**GPU metric descriptors** (replacing the four host-average ones):

| Chart | Series |
|-------|--------|
| GPU utilization | All GPUs + one per vendor present (from `gpuVendors`) |
| GPU memory utilization | All + per vendor |
| GPU memory used (MB) | per vendor |
| GPU temperature | per vendor (gaps where null) |
| GPU power | per vendor + total |
| AMD hotspot temperature | AMD only (shown when AMD data exists) |
| AMD GTT used | AMD only |
| Apple GPU working set / memory in use | Apple only |
| Apple thermal state | Apple only (step chart, nominal→critical) |
| Qualcomm GPU memory used vs budget | Qualcomm only |

Vendor-only charts are hidden when the selected range has no data for that vendor.

**Tooltips:** one row per visible series, plus the existing range/device-count rows per vendor.

**Filters:**

- `GPU vendor` select (Any / NVIDIA / AMD / Apple / Qualcomm), sent to search and rollups.
- `amdAvailable`, `appleGpuAvailable`, and `qualcommAvailable` tri-state selects next to `nvidiaAvailable`.

**Summary cards:** "Average GPU" and "Average GPU temp" respect the vendor filter. Add a per-vendor mini-breakdown line.

**Records table:** add a `GPU vendor` column. The sample detail JSON modal already shows the vendor sections.

`weightedAverage` must skip null buckets rather than counting them as 0. Fix and test it.

### 6.3 Shared code

- `dashboard/src/i18n/formatters.js`: `formatOptional`, `formatThermalState`, `formatVendor`, `formatSource`.
- New `dashboard/src/components/GpuVendorSection.jsx`: shared card shell (header pills, table, expandable rows). Plus `NvidiaDeviceRows.jsx`, `AmdDeviceRows.jsx`, `AppleGpuPanel.jsx`, `QualcommDeviceRows.jsx` with the vendor-specific columns.
- New `dashboard/src/components/UnifiedMemoryBar.jsx`.
- New `dashboard/src/components/charts/MultiSeriesLineChart.jsx`, extracted from the analytics view.
- `useTelemetry.js`: no change. It already fetches the full snapshot and capabilities.

### 6.4 i18n (all nine locales: `en`, `de`, `es`, `fr`, `it`, `ja`, `pt`, `yue`, `zh`)

```
sections.{nvidia,amd,apple,qualcomm}
descriptions.{nvidia,amd,apple,qualcomm}
vendors.{nvidia,amd,apple,qualcomm,intel,mixed,unknown}
sources.{dcgmExporter,amdSmiLibrary,amdSysfs,amdMetricsExporter,appleNative,windowsGraphicsKernel,drmMsm,kgsl}
utilizationScope.{device,allProcesses,ownProcesses}
appleFeatures.{ioKit,metal,ioReport,hidSensors}
thermalState.{nominal,fair,serious,critical}
labels.{source,gapFill,libraryVersion,hotspotTemperature,memoryTemperature,memoryActivity,gtt,
        partition,eccCorrectable,eccUncorrectable,gpuCores,metalFamily,rendererUtilization,
        tilerUtilization,gpuMemoryInUse,recommendedWorkingSet,wiredLimit,frequency,
        activeResidency,thermalState,details,allGpus,memoryBudget,sharedMemory,dedicatedMemory,
        engine3d,engineCompute,engineCopy,engineVideoDecode,engineVideoEncode,maxFrequency}
overview.cards.{gpuUtilization,gpuPower}
overview.capabilities.{nvidia,amd,apple,qualcomm}
analytics.metrics.{gpuMemoryUsed,amdHotspot,amdGtt,appleWorkingSet,appleThermalState,qualcommMemoryBudget}
analytics.filters.{gpuVendor,amdAvailable,appleGpuAvailable,qualcommAvailable}
analytics.table.gpuVendor
messages.{nvidiaMissing,amdMissing,appleMissing,qualcommMissing,setupGuide}
```

The existing `descriptions.gpu` and `messages.gpuMissing` are reworded vendor-neutral. English is final. Other locales are drafted and flagged in the PR for native review.

## 7. Packaging and deployment

- **Linux bare metal:** the systemd unit template in `BARE_METAL_DEPLOYMENT.md` and `scripts/linux/install-at-startup.sh` adds `SupplementaryGroups=render video` and, when ROCm is installed, `Environment=LD_LIBRARY_PATH=/opt/rocm/lib`. The install script warns if the user is not in `render`/`video`.
- **macOS:** the LaunchAgent (`scripts/macos/launchd.sh`) needs no change. All Apple APIs work as a normal user. Remove any mention of sudo.
- **Docker:** the image does not bundle ROCm. In containers, AMD resolves to sysfs (with `/sys` visible) or to the exporter. Add a commented `compose.yaml` example for passing `/dev/kfd` and `/dev/dri` plus a ROCm-based image variant as a documented option. The Apple section is unsupported in Docker.
- **Windows (x64 and Arm64):** the Windows backend uses only in-box DLLs (`dxgi.dll`, `pdh.dll`, `gdi32.dll`) and works as a standard user, so the Startup-folder shortcut needs no change. RigMonitor must run natively on Snapdragon, not under x64 emulation. `go.bat` and `scripts/windows/*.bat` do framework-dependent builds with no explicit RID, so they run natively when the Arm64 .NET SDK is installed. The Windows docs say to install the Arm64 SDK, and the release checklist checks `RuntimeInformation.ProcessArchitecture == Arm64` on the device.
- **Linux Arm64 (Snapdragon / Qualcomm boards):** no extra groups for devfreq/thermal/sysfs. fdinfo scope limits are documented, and no extra capabilities are granted.

## 8. Testing

### 8.1 Fixtures

`src/Test.Shared/Fixtures/Gpu/`:

| Fixture | Contents |
|---------|----------|
| `dcgm/*.prom` | existing inline samples moved to files, plus GB10 and MIG |
| `amd-smi/*.json` | recorded **library call results** (per call, per device) from MI300X (SPX and CPX), MI250, RX 7900 XTX, Strix Halo, including `NOT_SUPPORTED` statuses. Replayed by a fake `IAmdSmiNative`. |
| `amd-sysfs/<host>/…` | trimmed `/sys/class/drm` trees for RX 7900 XTX, Strix Halo, MI300X |
| `amd-exporter/*.prom` | Device Metrics Exporter output |
| `apple/<chip>.json` | recorded `IAppleGpuNative` DTOs (IOKit property dictionaries, Metal values, IOReport channel samples, HID sensor lists) for M1, M2 Pro, M3 Max, M4 Pro, and an Intel Mac with Radeon Pro |
| `windows/<device>.json` | recorded `IWindowsGpuNative` DTOs (DXGI adapter descs, `QueryVideoMemoryInfo`, PDH instance/value sets, D3DKMT perf data) for Snapdragon X Elite (Adreno X1-85), X Plus, X2, plus an AMD and an Intel Windows adapter for the vendor-neutral path |
| `qualcomm-linux/<board>/…` | trimmed `/sys` (drm, devfreq, thermal) and `/proc/*/fdinfo` trees from a Snapdragon X laptop on upstream Linux and from a `kgsl` BSP board |

Add `scripts/capture-gpu-fixtures.sh` (Linux/macOS). It runs a small `RigMonitor.Server --dump-gpu-fixtures <dir>` mode that records native DTOs through the real native layer and scrubs serials/UUIDs/hostnames, so contributors can add hardware without writing code.

### 8.2 Unit tests (`Test.Xunit`, run on every OS)

**PrometheusTextParserTests:** labels with escaped quotes/commas; HELP/TYPE/blank lines; timestamps; `NaN`/`±Inf`; CRLF; unlabeled metrics. DCGM tests must pass unchanged after the extraction.

**NvidiaDcgmGpuTelemetryProviderTests** (extended): existing tests; `vendor`/`source` set; absent temperature/power → null; GB10 → `unifiedMemory`; MIG → distinct devices with `migProfile`; optional ECC/memory-temp metrics.

**AmdSmiLibraryBackendTests** (fake `IAmdSmiNative`): enumeration across sockets; SPX vs CPX partitions; every metric mapped and unit-converted; `NOT_SUPPORTED` → null; other error codes → provider error without losing other devices; library version outside the supported range → backend refuses and `auto` falls back to sysfs; init failure → probe false; static info cached (identity calls once across N samples); dispose calls shutdown exactly once.

**AmdNativeStructLayoutTests:** `Marshal.SizeOf` / field offsets for every interop struct match the header values.

**AmdSysfsBackendTests** (temp-dir trees): card discovery skipping connectors and non-AMD vendors; hwmon by label; all unit conversions; `power1_average` → `power1_input` fallback; unreadable file → field null; GTT and APU detection.

**AmdGpuTelemetryProviderTests** (backend selection and merge): `auto` order library → sysfs → exporter; forced `AmdSource` values; **BDF merge**: library null hotspot + sysfs hotspot → filled, and `fieldSources` records `amdSysfs`; library and sysfs report the same device → one device, never two; gap-fill disabled → nulls stay null; exporter backend parsing (MI300X, partitions, edge→junction fallback, derived free VRAM, `clock_type` split, wrong-vendor body → null).

**AppleGpuTelemetryProviderTests** (fake `IAppleGpuNative` with chip fixtures): M1/M2/M3/M4 map utilization, renderer/tiler, memory in use, core count, Metal family, working set, wired limit; IOReport energy deltas → watts (including counter reset and first-sample-has-no-delta → null); P-state residency → weighted frequency with the M1–M3 vs M4 table; HID sensors → average/max GPU temperature by chip name table and the generic fallback; each feature missing (IOReport absent, HID absent, Metal absent) → only those fields null, section still ok; kill-switch settings honoured; Intel Mac fixture → vendor `amd`, power/temp null; thermal state mapping.

**AppleGpuSamplerTests:** background sampling with `ManualTimeProvider`; snapshot reads never block; stale sampler data (sampler stopped) is reported as null, not as the last value forever.

**CoreFoundationConverterTests** (macOS-only, `[Trait("Platform","macos")]`): CFDictionary/CFNumber/CFString/CFData round-trip conversions on real CF objects.

**WindowsGraphicsKernelBackendTests** (fake `IWindowsGpuNative`): adapters routed by `VendorId` (Qualcomm `0x5143` → `qualcomm`, AMD `0x1002` → `amd`, Intel `0x8086` → reserved/skipped, NVIDIA skipped when DCGM is available); software adapter skipped; PDH instance names parsed for LUID/engine type, including multiple physical adapters; utilization = max(3D, Compute) per adapter; first sample (no rate yet) → null; memory used/budget from both segment groups; D3DKMT perf data zero/absent → null rather than 0; `WindowsGenericGpuEnabled=false` → only Qualcomm reported.

**WindowsNativeStructLayoutTests:** `DXGI_ADAPTER_DESC1`, `DXGI_QUERY_VIDEO_MEMORY_INFO`, `D3DKMT_ADAPTER_PERFDATA`, and `D3DKMT_QUERYADAPTERINFO` sizes and offsets on x64 and Arm64.

**DrmMsmBackendTests / KgslBackendTests** (temp-dir `/sys` and `/proc` trees): discovers the msm render node and model from `compatible` via `AdrenoModelTable`; devfreq `cur_freq`/`max_freq` (Hz → MHz); `trans_stat` delta → busy residency, scope `device`; fdinfo `drm-engine-gpu` deltas summed across PIDs for the right DRM minor only, scope `allProcesses` vs `ownProcesses` based on which PIDs were readable; process exit between samples (counter disappears) is handled without negative deltas; thermal zones matched by `gpu*` type, millidegrees → C; kgsl `gpu_busy_percentage`, `gpuclk`, `temp`; no Qualcomm node → probe false.

**QualcommGpuTelemetryProviderTests:** `auto` resolves to `windows` on Windows and to `drmMsm` before `kgsl` on Linux; forced sources; `unifiedMemory` always set; common metrics mapped (memory = shared + dedicated, total = budget on Windows or host RAM on Linux).

**GpuAggregatorTests:** single vendor passthrough; mixed → `vendor = mixed`, ordinals 0..n-1, sorted by vendor; vendor-specific fields absent from the aggregate; empty when no vendor sections.

**TelemetryServiceCollectionStatusTests** (extended): each vendor section gets ok / unsupported / unavailable / error / stale, the same matrix as the existing tests; derived `collection.gpu` precedence rules (section 5.2); selector matrix from section 5.2 (`?gpu` collects all but emits only `gpu`; `?amd` emits only `amd`); vendor sections collected in parallel; unified-memory fallback for GB10 (existing test), Strix Halo, and Apple; fallback never overrides reported memory.

**TelemetryRequestParserTests:** new `nvidia`/`amd`/`apple`/`qualcomm` keys, `=false` handling, interaction with `gpu`.

**RuntimeCapabilitiesServiceTests** (new): platform gating matrix; `NvidiaEnabled`/`AmdEnabled`/`AppleGpuEnabled = false` → never probed; `gpuAvailable` = OR of vendors; `amdSource` and `appleFeatures` populated; probes run in parallel (bounded total time with slow fakes).

**PersistenceTests** (extended):
- round-trip NVIDIA, AMD, Apple, Qualcomm, and mixed snapshots. Vendor columns populated, nullable values stored as NULL, `vendordetailjson` populated.
- **Migration from a 0.2.1 database:** create the old schema from DDL copied into the test, insert NVIDIA rows, initialize → columns added, rows backfilled `nvidia`/`dcgmExporter`, still searchable and rollup-able.
- migration idempotent across repeated starts.
- search filters `gpuVendor`, `amdAvailable`, `appleGpuAvailable`, `qualcommAvailable`.
- rollup `gpuVendors` breakdown on a mixed host: per-vendor utilization averages, **power summed per sample then averaged**, temperature averages ignoring NULL, AMD hotspot/GTT and Apple working set/thermal state values.
- rollup with a `GpuVendor` filter recomputes host-level fields from that vendor only.
- retention pruning cascades to the new columns (no orphan rows).
- legacy `snapshotjson` (uppercase vendor, `metrics.xidErrors`) loads through the detail endpoint.

**JsonSerializationTests** (extended): camelCase enums; nulls omitted; vendor device subclasses serialize their extra fields in vendor sections but only base fields in the `gpu` aggregate; legacy converter.

**Settings tests** (`TelemetrySettingsClampTests`, `DashboardSettingsClampTests`, `Test.Automated/SettingsFileCreationTests`): new defaults written; `AppleSamplerIntervalMs` and `WindowsGpuSamplerIntervalMs` clamps; invalid `AmdSource`/`QualcommSource` → `auto`; existing files gain keys without losing values; `HideUnsupportedGpuSections` default.

**Observability tests** (`ObservabilityIntegrationTests`, `ObservabilityPipelineTests`, `TelemetryLoggingTests`): native integration spans with outcome tags; new section label values; feature-availability gauge; native error counter label set stays fixed (no device ids); a startup log line per vendor.

**DashboardStaticFileTests:** unchanged, but run against the new bundle.

### 8.3 Native integration tests (`Test.Automated`, gated by trait, run on real hardware)

| Trait | Runs on | Asserts |
|-------|---------|---------|
| `Hardware=apple` | macOS Apple Silicon (CI) | ≥1 accelerator; utilization in 0..100; memory in use > 0; working set > 0; Metal family set; IOReport power ≥ 0 after two samples; ≥1 GPU temperature sensor in 10..120 C; thermal state set |
| `Hardware=apple-soak` | macOS (nightly) | 10,000 samples: resident memory growth < 5 MB (CF leak check); no handle growth |
| `Hardware=amd` | Linux with AMD GPU (self-hosted / manual) | library init or sysfs probe succeeds; per-device values in range; library vs sysfs utilization within tolerance for the same BDF |
| `Hardware=nvidia` | Linux with DCGM (manual) | unchanged behaviour |
| `Hardware=qualcomm-windows` | Windows on Snapdragon (manual / self-hosted) | process is Arm64; ≥1 Qualcomm adapter; utilization in 0..100 after two samples; budget > 0; PDH counters resolve for the adapter LUID |
| `Hardware=qualcomm-linux` | Snapdragon on upstream Linux (manual) | msm node found; frequency > 0; ≥1 GPU thermal zone in range |
| `Hardware=windows-gpu` | any Windows CI runner with a WARP/real adapter | DXGI/PDH interop loads and returns well-formed DTOs (guards the COM/PDH layer even without Qualcomm hardware) |

These tests are skipped, not failed, when the hardware is absent. The default `dotnet test` filter excludes `Hardware`.

### 8.4 CI

- Existing Linux/Windows jobs: all unit tests (parsers, mappers, and fakes cover AMD, Apple, and Qualcomm logic on every OS).
- **New macOS Apple Silicon job** (GitHub-hosted `macos-14`/`macos-15` arm64): unit tests plus `Hardware=apple`. This is the guard that catches macOS updates breaking the undocumented Apple APIs. The nightly job adds `apple-soak`.
- Dashboard job: `npm ci && npm test && npm run build`.
- AMD hardware: manual release checklist (section 8.6), or a self-hosted runner if one becomes available.
- **Windows Arm64 job** (GitHub-hosted `windows-11-arm`): builds and runs all unit tests natively on Arm64, plus `Hardware=windows-gpu` to check the interop layer. Hosted Arm runners have no Adreno GPU, so `Hardware=qualcomm-windows` runs on a Snapdragon dev kit (self-hosted runner recommended, otherwise the manual checklist).
- **Linux Arm64 job** (`ubuntu-24.04-arm`): unit tests on Arm64.

### 8.5 Dashboard tests (new harness)

Add `vitest`, `@testing-library/react`, `jsdom` (dev dependencies) and an `npm test` script.

- `formatters.test.js`: `formatOptional`, thermal state, vendor/source names.
- `GpuVendorSection.test.jsx`: NVIDIA, AMD, Apple, Qualcomm rendering from fixture snapshots. Placeholder states (unsupported/unavailable/error/stale) per vendor. `HideUnsupportedGpuSections`. No `0 C` for null temperature. Expandable rows. AMD gap-fill badge. Apple unified memory bar values.
- `HomeView.test.jsx`: four sections in fixed order; Qualcomm utilization-scope badge; capability pills; GPU RAM/utilization/power cards across vendors; mixed host.
- `MultiSeriesLineChart.test.jsx`: series per vendor, legend toggles, null gaps, tooltip rows.
- `TelemetryAnalyticsView.test.jsx`: vendor filter in rollup/search payloads; vendor-only charts hidden without data; `weightedAverage` skips nulls.
- `i18n.parity.test.js`: every locale has exactly `en.js`'s key set.

### 8.6 Manual release checklist

| Host | Expectation |
|------|-------------|
| Linux + NVIDIA RTX (DCGM) | NVIDIA section is today's data. AMD, Apple, and Qualcomm sections show unsupported. History intact. |
| NVIDIA GB10 / DGX Spark | shared memory in the NVIDIA section and the aggregate |
| Linux + MI300X, ROCm, SPX and CPX | `amdSmiLibrary` source, correct device/partition count, ECC, hotspot |
| Same, ROCm removed | `amdSysfs` after restart |
| Radeon RX 7900 XTX, no ROCm | `amdSysfs`, temperatures and power present |
| Ryzen AI Max (Strix Halo) | `unifiedMemory`, GTT shown, shared memory in the aggregate |
| Mixed NVIDIA + AMD | both sections, `gpu.vendor = mixed`, per-vendor chart series correct |
| Apple M1 and M4 Pro (native) | all Apple fields populated without sudo, thermal state changes under load |
| Apple with `AppleIOReportEnabled=false` | power/frequency null, section ok |
| Intel Mac + Radeon Pro | best-effort fields, nulls rendered as `—` |
| Docker on Linux, AMD host | sysfs or exporter source |
| Docker Desktop on macOS | Apple unsupported, documented |
| Windows + NVIDIA | unchanged |
| Windows on Snapdragon X Elite / X2 (native Arm64) | Qualcomm section: utilization tracks Task Manager within ±5 pts, shared memory used/budget, frequency/temp when the driver provides perf data |
| Windows x64 with AMD or Intel GPU | basic telemetry via the Windows backend (AMD into the AMD section, `source = windowsGraphicsKernel`) |
| Snapdragon X laptop on upstream Linux | `drmMsm` source, frequency and temp, utilization scope reported honestly |
| Qualcomm board with `kgsl` BSP kernel | `kgsl` source |
| Upgrade a 0.2.1 database | backfilled `nvidia`, charts render history |

## 9. Documentation

| Document | Change |
|----------|--------|
| `INSTALLING_AMD_GPU_TELEMETRY.md` (new) | What RigMonitor reads (library, sysfs, exporter) and how `auto` chooses. Ubuntu 24.04: amdgpu driver, ROCm install (for the library), `render`/`video` groups, `/dev/kfd`. Verification (`ls /opt/rocm/lib/libamd_smi.so*`, `cat /sys/class/drm/card*/device/gpu_busy_percent`, `curl /v1/capabilities`). Partition modes. Optional exporter setup for containers. Troubleshooting (library version unsupported, permission denied, missing hwmon, APU memory). |
| `APPLE_GPU_TELEMETRY.md` (new) | Why "Metal" is a graphics API and not a telemetry source, and which native frameworks RigMonitor uses. What each field means. Unified memory, working set, and `iogpu.wired_limit_mb` for model placement. Undocumented APIs and the kill-switch settings. Intel Mac limitations. Docker limitation. No sudo required. |
| `QUALCOMM_GPU_TELEMETRY.md` (new) | Supported devices (Snapdragon X-series on Windows, upstream `drm/msm` and `kgsl` on Linux). What each native source provides and why there is no vendor library. Installing the Arm64 .NET SDK so RigMonitor runs natively. Shared memory and budget for placement. Utilization scope on Linux and why RigMonitor doesn't request `CAP_SYS_PTRACE`. Driver perf-data caveats (frequency/temperature may be absent). Verification steps (Task Manager comparison, `/v1/capabilities`). Note on the vendor-neutral Windows backend for AMD/Intel. |
| `INSTALLING_DCGM.md` | Cross-links to the AMD, Apple, and Qualcomm guides. `NvidiaEnabled`. |
| `README.md` | Features ("NVIDIA, AMD, Apple, and Qualcomm GPU telemetry"). Selectors list (`nvidia`, `amd`, `apple`, `qualcomm`). Windows on Arm support. Section-states matrix note for derived `gpu`. Settings list. Guide links. Dashboard sections. `npm test`. |
| `REST_API.md` | Vendor sections with full field tables and examples (NVIDIA, AMD with gap-fill, Apple, Qualcomm on Windows and Linux, mixed). Reshaped `gpu` aggregate. Derived `collection.gpu` rules. Capabilities. Search filters. Rollup `gpuVendors`. Nullability. Breaking-change notes. |
| `BARE_METAL_DEPLOYMENT.md` | AMD prerequisite section. systemd `SupplementaryGroups` and `LD_LIBRARY_PATH`. Verification commands. macOS bare-metal note. Linux Arm64 / Qualcomm note. |
| `TELEMETRY.md` | New sections, integration services/operations, native feature gauge, error counter, span tree for vendor sections. |
| `CHANGELOG.md` | `0.3.0-alpha` entry with features and breaking changes. |
| `RigMonitor.postman_collection.json` | Requests for `?nvidia`, `?amd`, `?apple`, `?gpu`. Vendor-filtered search/rollups. Example responses. |
| `assets/grafana/*.json` | Section 5.8. |
| `docker/compose.yaml` | Commented AMD device passthrough / exporter examples. |
| XML doc comments | All new public types and members, in the existing style. |

## 10. Breaking changes

Approved for `0.3.0-alpha`:

1. `gpu.vendor`: `"NVIDIA"` → `nvidia` / `amd` / `apple` / `qualcomm` / `mixed`.
2. `gpu.exporterEndpoint` removed (now in vendor sections).
3. `gpu.devices[].metrics.xidErrors` and `gpu.devices[].migProfile` move to `nvidia.devices[]`.
4. `temperatureCelsius`, `powerUsageWatts`, `smClockMHz`, `memoryClockMHz` are omitted when unknown instead of `0`.
5. History/rollup GPU power and temperature averages are `null` when no device reports them.
6. `IDcgmExporterClient` deprecated (`IPrometheusTextClient`). `NvidiaDcgmGpuTelemetryProvider` moves to `RigMonitor.Telemetry.Gpu.Nvidia`.

## 11. Phased delivery

Each phase is one PR that builds, passes all tests, and keeps the dashboard working.

| Phase | Scope | Exit criteria |
|-------|-------|---------------|
| **1. Foundation** | Enums, models, vendor sections and selectors, `GpuAggregator`, derived `collection.gpu`, capability shape, settings. NVIDIA provider moved into the `nvidia` section. Prometheus parser extraction. Persistence schema, migration, backfill, vendor rollup breakdown. Legacy JSON converter. Dashboard: vendor-section shell, NVIDIA section, `formatOptional`, multi-series chart component, vitest harness with i18n parity test. | NVIDIA hosts show the same data in the new NVIDIA section. All existing and new tests green. |
| **2. AMD** | sysfs backend, native `libamd_smi` backend, BDF merge, exporter backend, AMD section UI and charts, AMD guide, bare-metal and systemd changes. | AMD checklist rows pass (fixture-only for unavailable hardware, noted in the PR). |
| **3. Apple** | Native interop layer (CF, IOKit, ObjC/Metal, sysctl, Foundation, IOReport, IOHID), sampler, chip tables, Apple section UI (unified memory bar, thermal state) and charts, Apple guide, macOS CI and soak jobs. | macOS CI green. Apple checklist rows pass. |
| **4. Qualcomm** | Windows native layer (DXGI COM, PDH, D3DKMT) as a vendor-neutral backend plus sampler; `drm/msm` and `kgsl` backends; Adreno model table; Qualcomm section UI and charts; AMD/Intel-on-Windows routing; Qualcomm guide; Windows Arm64 and Linux Arm64 CI jobs. | Arm64 CI green. Snapdragon checklist rows pass (on a dev kit or self-hosted runner). |
| **5. Integration and release** | Analytics vendor filters and vendor-only charts, overview cards, Grafana dashboards, Postman, all locales, README/REST_API/TELEMETRY/CHANGELOG, version bump. | Full checklist (section 8.6) complete. |

Phases 2, 3, and 4 can run in parallel after Phase 1.

## 12. Risks and mitigations

| Risk | Mitigation |
|------|------------|
| A macOS update changes an undocumented API (IOReport/IOHID keys, sensor names) | Runtime loading plus per-feature null fallback; chip/sensor tables in managed code; macOS CI on every PR plus nightly; kill-switch settings; fixture capture tool for quick updates. |
| CoreFoundation leaks in a long-running daemon | `SafeHandle` ownership; soak test with a memory budget. |
| `libamd_smi` ABI changes across ROCm releases | Version gate with a declared supported range; struct layout tests; automatic sysfs fallback outside the range. |
| AMD devices counted twice across backends | Merge keyed on PCI BDF; dedupe test. |
| Mixed-host rollups averaging across vendors | Per-vendor breakdown with two-level aggregation; vendor filter recomputes host fields. |
| Native calls slow the snapshot | Vendor sections run in parallel; Apple deltas come from a background sampler; AMD static info cached; spans make cost visible. |
| Startup slowed by probes | Parallel, time-bounded probes; platform gating; disabled vendors are never probed. |
| Permissions on Linux (`/dev/kfd`, `render` group) | Install-script warning; systemd `SupplementaryGroups`; clear status message in `collection.amd.message`. |
| Qualcomm driver perf data (frequency/temperature/power) missing or zero on some driver releases | Every field nullable; zero treated as "not reported" where physically impossible; per-field source map in the UI; driver version recorded with each sample for correlation. |
| Linux fdinfo utilization under-reports other users' work | Prefer devfreq busy residency; report `utilizationScope` explicitly; never present partial data as device-wide. |
| No Adreno hardware in hosted CI | Hosted Arm64 runners cover build, unit tests, and the interop layer (`Hardware=windows-gpu`); a Snapdragon dev kit as a self-hosted runner covers the rest. |

## 13. Follow-ups (out of scope)

- **NVIDIA NVML native backend**, the counterpart to native AMD, Apple, and Qualcomm, with DCGM kept for datacenter/MIG/remote cases.
- Periodic capability re-probe (no restart after installing drivers or exporters).
- Vendor libraries on Windows for richer AMD (ADLX) and Intel (IGCL) data, beyond the basics from the Windows backend. A dedicated `intel` section.
- **Qualcomm Hexagon NPU** telemetry (Windows 11 exposes NPUs as compute-only MCDM adapters through the same D3DKMT/PDH interfaces, so the Windows backend can grow an `npu` device kind). Qualcomm Cloud AI 100 (`qaic` driver) accelerators.
- Per-process GPU memory attribution alongside Ollama/vLLM.
