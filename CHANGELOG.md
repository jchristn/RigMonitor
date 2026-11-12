# Changelog

## 0.3.0-alpha

- Added AMD, Apple, and Qualcomm GPU telemetry alongside NVIDIA, each in its own `/v1/telemetry` section (`nvidia`, `amd`, `apple`, `qualcomm`) with its own selector key and collection status; `gpu` is now a vendor-neutral aggregate derived from them, and vendor sections are collected in parallel
- AMD: in-process ROCm AMD SMI library (`libamd_smi.so`) with per-field gap-fill from `amdgpu` sysfs matched by PCI address, sysfs-only fallback for hosts without ROCm, and the AMD Device Metrics Exporter as a last resort; reports activity, VRAM, GTT, edge/hotspot/memory temperatures, power, clocks, ECC, and partitions; APUs (Ryzen AI Max, MI300A, ...) combine VRAM and GTT
- Apple: in-process IOKit, Metal, Foundation, IOReport, and IOHID (no `sudo`, no helper processes) for device/renderer/tiler utilization, unified memory in use, Metal's recommended working set, GPU power, active frequency and residency, temperature (GPU sensors, else SoC die sensors), and thermal state; IOReport and IOHID are sampled in the background
- Qualcomm: Windows on Snapdragon through DXGI, PDH, and D3DKMT (also used for AMD adapters on Windows), and Linux through `drm/msm` (devfreq, fdinfo, thermal zones) or `kgsl`, with an explicit `utilizationScope`
- Added `Telemetry.NvidiaEnabled`, `AmdEnabled`, `AmdSource`, `AmdSmiLibraryPath`, `AmdSysfsGapFillEnabled`, `AmdSysfsRoot`, `AmdMetricsExporterUrl`, `AppleGpuEnabled`, `AppleIOReportEnabled`, `AppleHidSensorsEnabled`, `AppleSamplerIntervalMs`, `QualcommEnabled`, `QualcommSource`, `QualcommSysfsRoot`, `QualcommProcRoot`, `WindowsGenericGpuEnabled`, `WindowsGpuSamplerIntervalMs`, and `Dashboard.HideUnsupportedGpuSections`
- `/v1/capabilities` adds `amdAvailable`, `appleGpuAvailable`, `qualcommAvailable`, `gpuAvailable`, `amdSource`, `qualcommSource`, `appleFeatures`, `gpuProviders` (probe result and reason per vendor), `amdMetricsExporterUrl`, and `hideUnsupportedGpuSections`
- Telemetry history stores vendor, source, and vendor-specific GPU columns; search adds `gpuVendor`, `amdAvailable`, `appleGpuAvailable`, and `qualcommAvailable`; roll-ups add a `gpuVendor` filter and a per-vendor `gpuVendors` breakdown aggregated per sample then per bucket; existing databases migrate in place and existing GPU rows are tagged `nvidia`
- Dashboard: NVIDIA, AMD, Apple, and Qualcomm sections with vendor-specific columns, expandable device details, a unified memory bar, setup-guide links, and a "Show unsupported GPU vendors" toggle; per-vendor capability pills and GPU utilization/power cards; analytics charts draw one series per vendor with a legend, add vendor-only charts (AMD hotspot and GTT, Apple working set and thermal state, Qualcomm memory budget), vendor filters, and a vendor column; all nine locales updated
- Added a Vitest test harness for the dashboard, including a locale key-parity test
- Added native GPU observability: `rigmonitor.gpu.native.feature.available`, `rigmonitor.gpu.native.errors`, integration spans for each GPU source, and `stage:<vendor>` spans under `stage:gpu`
- Added `INSTALLING_AMD_GPU_TELEMETRY.md`, `APPLE_GPU_TELEMETRY.md`, and `QUALCOMM_GPU_TELEMETRY.md`; the Linux systemd unit now sets `SupplementaryGroups=render video` and, when ROCm is installed, `LD_LIBRARY_PATH=/opt/rocm/lib`
- Fixed SQLite query results whose aggregate columns were typed as `byte[]` when the first row was null
- Breaking: `gpu.vendor` is now a camelCase enum (`nvidia`, `amd`, `apple`, `qualcomm`, `mixed`) instead of `"NVIDIA"`; `gpu.exporterEndpoint` moved to `nvidia.endpoint`; `xidErrors` and `migProfile` moved to `nvidia.devices[]`; `temperatureCelsius`, `powerUsageWatts`, `smClockMHz`, and `memoryClockMHz` are omitted when unknown instead of `0`; history and roll-up GPU power and temperature are `null` when no device reported them; `IGpuTelemetryProvider` was replaced by per-vendor provider interfaces. Snapshots stored by earlier versions still load.

## 0.2.1-alpha

- Updated NuGet dependencies: Watson 7.1.1 -> 7.2.2, SyslogLogging 2.2.2 -> 2.3.1, Microsoft.Data.Sqlite 10.0.11 -> 10.0.12, System.Diagnostics.PerformanceCounter 10.0.11 -> 10.0.12, System.Management 10.0.11 -> 10.0.12
- Updated test dependencies: Touchstone.Core, Touchstone.XunitAdapter, and Touchstone.NunitAdapter 0.1.12 -> 0.2.0, NUnit 4.6.1 -> 5.0.0, NUnit.Analyzers 4.14.0 -> 4.15.0, NUnit3TestAdapter 6.2.0 -> 6.3.0, Microsoft.NET.Test.Sdk 18.9.0 -> 18.10.1, coverlet.collector 10.0.1 -> 10.1.0
- No code changes were required; RigMonitor does not use Watson access control (whose denials now return 403 instead of 500) or SyslogLogging `DisposeAsync`

## 0.2.0-alpha

- Added production observability for RigMonitor itself: a `RigMonitor` `Meter` and `ActivitySource` (BCL only, no exporter dependency in `RigMonitor.Core` or `RigMonitor.Telemetry`) covering the snapshot workflow and every section, the background collector (job, queued/collect/persist stages, last success), retention, every outbound integration (DCGM exporter, Ollama, vLLM, Utilyze, SQLite, subprocesses), the SQLite concurrency slot, startup, capabilities, errors by type, build info, and safe configuration gauges
- Added a Radiant 0.1.2 telemetry host in `RigMonitor.Server` that subscribes to the `RigMonitor` and `Watson` sources and exports over OTLP, an in-process Prometheus scrape endpoint (default `127.0.0.1:9464`), and optionally Loki, with .NET runtime metrics and second-based histogram buckets
- Enabled Watson's built-in HTTP telemetry and W3C trace context propagation explicitly
- Added the `Observability` settings section and `RIGMONITOR_OBSERVABILITY_*` environment variable overrides
- Forwarded application log lines to Loki with trace correlation when Loki export is enabled
- Added Prometheus, Tempo, Loki, and Grafana to `docker/compose.yaml` with healthchecks, dependency ordering, and an explicit `rigmonitor` project name; added `curl` to the runtime image for the healthcheck
- Added six Grafana dashboards provisioned into a RigMonitor folder: Overview, HTTP, Snapshots & Sections, Collector & Retention, Integrations, and Runtime
- Added an External services card to the dashboard home page linking to Grafana, Prometheus, Tempo, and Loki, translated into all supported languages
- Added `docker/update.bat` and `docker/update.sh`
- Added `TELEMETRY.md` with the full metrics and spans catalog, configuration, dashboards, and recommended alerts
- Added telemetry tests using in-memory meter and activity listeners
- Changed a timed-out (not cancelled) background collection to be logged and retried on the next tick instead of silently ending the collection loop

## 0.1.0

- Initial RigMonitor implementation
- Added Watson 7 telemetry API host
- Added same-port React dashboard
- Added DCGM-based NVIDIA telemetry support
- Added Ollama detection and telemetry
- Added optional vLLM Prometheus telemetry support and dashboard card
- Added optional Utilyze sidecar telemetry support for GPU SOL, bandwidth, and attainable ceiling metrics
- Added dashboard Utilyze badges and telemetry card
- Added debug as the default minimum logging severity for generated settings
- Added settings file normalization on startup so existing `rigmonitor.json` files receive newly introduced default properties
- Added debug log messages for automated background telemetry collection start, scheduling, and persisted samples
- Added configurable telemetry persistence using SQLite with provider-neutral database interfaces
- Added PrettyId K-sortable telemetry sample IDs with prefixed IDs capped at 32 characters
- Added persisted hostname support and default `localhost` fallback for telemetry history tables
- Added background collection cadence and retention pruning settings with 15-second collection and 30-day default retention
- Added telemetry history enumerate, search, detail, roll-up, status, and delete APIs
- Fixed roll-up bucket filling for fractional-second time ranges so charts do not render empty buckets when samples match the range
- Added dashboard analytics page for filtering, charting, paging, and inspecting historical telemetry samples
- Added analytics quick-range highlighting and a JSON viewer for the current analytics view
- Refined analytics roll-up chart sampling, Y-axis labels, fixed-size axis text, and hover tooltips
- Added contextual roll-up chart hover details for CPU cores, memory usage, paired network/disk metrics, GPU ranges, and runtime queues
- Added server output cleanup scripts for local runtime settings, logs, and SQLite database assets
- Added root native run scripts that build the dashboard, build the server, prepare local runtime directories, and launch RigMonitor
- Added Docker build and compose assets
- Added Docker runtime data persistence for settings, logs, SQLite databases, and SQLite WAL/SHM sidecars
