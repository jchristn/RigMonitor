# RIGMONITOR Implementation Plan

## Status
- Owner:
- Started:
- Target:
- Completed:
- Notes:

## Objective
Build `RigMonitor` as a cross-platform long-running daemon for Linux, Windows, and macOS that exposes host telemetry through Watson 7 APIs and, when enabled, a same-port dashboard. Telemetry collection must be a first-party subsystem inside this repository rather than a NuGet dependency, with the relevant cross-platform logic from `PerformanceStatistics` folded into a dedicated internal project and reshaped around RigMonitor's contract. NVIDIA GPU support is v1-only and must be implemented behind an extensible provider model so additional GPU vendors can be added later without breaking the API contract. The dashboard must use `assets/icon.png` as its visible application mark and `assets/icon.ico` as its favicon.

## Standards And Constraints
- Follow the repository requirements in `C:\code\claude\REPOSITORY_REQUIREMENTS.md`.
- Follow the C# style rules in `C:\code\claude\CODE_STYLE.md`.
- Follow the Watson 7 composition and server patterns in `C:\code\claude\BACKEND_ARCHITECTURE.md`.
- Follow the Touchstone-based test structure in `C:\code\claude\BACKEND_TEST_ARCHITECTURE.md`.
- Follow the React/Vite/fetch/i18n dashboard guidance in `C:\code\claude\FRONTEND_ARCHITECTURE.md` and `C:\code\claude\I18N.md`.
- Use `Watson` 7.x for the HTTP server.
- Use `SyslogLogging` for logging and mirror the settings/layout conventions used in the reference projects.
- Fold the relevant `PerformanceStatistics` functionality into this repo as first-party code under a dedicated telemetry project rather than using the NuGet package.
- GPU telemetry must only be included when DCGM exporter is available.
- The implementation must be settings-file backed, not database backed.
- License the repo under MIT.

## Working Assumptions
- No blocking questions were identified during planning.
- Target framework: `net10.0` unless a package compatibility issue forces a downgrade.
- Nullability: enabled in all new C# projects.
- Default settings filename: `rigmonitor.json`.
- Default listener hostname: `localhost`.
- Default listener port: `9000`.
- Default DCGM exporter URL: `http://localhost:9400/metrics`, but this must be configurable.
- Default Ollama base URL: `http://localhost:11434`, but this must be configurable.
- v1 is intentionally unauthenticated because the request did not specify auth; the default bind address of `localhost` is the primary safety control.
- The dashboard is served by the same Watson 7 host as the API and is disabled or enabled through settings.
- The daemon is a long-running executable. OS-native service manager wrappers can be documented after the core daemon is complete.

If any of the assumptions above change, update this plan before implementation starts.

## Repository Layout

```text
rigmonitor/
|-- .gitignore
|-- .dockerignore
|-- README.md
|-- CHANGELOG.md
|-- LICENSE.md
|-- rigmonitor.json
|-- RIGMONITOR.md
|-- docker/
|   |-- Dockerfile
|   |-- compose.yaml
|   |-- data/
|       |-- .gitkeep
|
|-- src/
|   |-- RigMonitor.sln
|   |
|   |-- RigMonitor.Core/
|   |   |-- RigMonitor.Core.csproj
|   |   |-- Constants.cs
|   |   |
|   |   |-- Enums/
|   |   |-- Models/
|   |   |   |-- TelemetrySnapshot.cs
|   |   |   |-- SystemTelemetry.cs
|   |   |   |-- CpuTelemetry.cs
|   |   |   |-- MemoryTelemetry.cs
|   |   |   |-- NetworkTelemetry.cs
|   |   |   |-- NetworkInterfaceTelemetry.cs
|   |   |   |-- DiskTelemetry.cs
|   |   |   |-- DiskVolumeTelemetry.cs
|   |   |   |-- GpuTelemetry.cs
|   |   |   |-- GpuDeviceTelemetry.cs
|   |   |   |-- GpuUtilizationTelemetry.cs
|   |   |   |-- RuntimeCapabilities.cs
|   |   |
|   |   |-- Settings/
|   |   |   |-- RigMonitorSettings.cs
|   |   |   |-- WebserverSettings.cs
|   |   |   |-- CorsSettings.cs
|   |   |   |-- LoggingSettings.cs
|   |   |   |-- SyslogServerSettings.cs
|   |   |   |-- DashboardSettings.cs
|   |   |   |-- TelemetrySettings.cs
|   |   |
|   |   |-- Services/
|   |       |-- Interfaces/
|   |           |-- ITelemetryService.cs
|   |           |-- ISystemTelemetryProvider.cs
|   |           |-- IGpuTelemetryProvider.cs
|   |           |-- IDcgmExporterClient.cs
|   |           |-- IMemoryInfoProvider.cs
|   |           |-- INetworkRateSampler.cs
|   |
|   |-- RigMonitor.Telemetry/
|   |   |-- RigMonitor.Telemetry.csproj
|   |   |
|   |   |-- Services/
|   |   |   |-- TelemetryService.cs
|   |   |   |-- RuntimeCapabilitiesService.cs
|   |   |   |-- DefaultMemoryInfoProvider.cs
|   |   |   |-- DefaultNetworkRateSampler.cs
|   |   |   |-- DcgmExporterClient.cs
|   |   |   |-- NvidiaDcgmGpuTelemetryProvider.cs
|   |   |
|   |   |-- Platform/
|   |       |-- Shared/
|   |       |   |-- PerformanceCounterSamplerBase.cs
|   |       |   |-- NetworkInterfaceSampler.cs
|   |       |   |-- DriveInventoryReader.cs
|   |       |
|   |       |-- Linux/
|   |       |   |-- LinuxSystemTelemetryProvider.cs
|   |       |   |-- ProcFileParser.cs
|   |       |
|   |       |-- Windows/
|   |       |   |-- WindowsSystemTelemetryProvider.cs
|   |       |
|   |       |-- Mac/
|   |           |-- MacSystemTelemetryProvider.cs
|   |           |-- MacSystemInfoReader.cs
|   |
|   |-- RigMonitor.Server/
|   |   |-- RigMonitor.Server.csproj
|   |   |-- Program.cs
|   |   |-- RigMonitorServer.cs
|   |   |
|   |   |-- Serialization/
|   |   |-- Routes/
|   |   |   |-- GeneralRoutes.cs
|   |   |   |-- TelemetryRoutes.cs
|   |   |   |-- DashboardRoutes.cs
|   |   |
|   |   |-- Services/
|   |   |-- Dashboard/
|   |   |   |-- StaticFileHandler.cs
|   |   |
|   |   |-- wwwroot/
|   |
|   |-- Test.Shared/
|   |-- Test.Automated/
|   |-- Test.Xunit/
|   |-- Test.Nunit/
|
|-- dashboard/
|   |-- package.json
|   |-- vite.config.js
|   |-- public/
|   |-- src/
|       |-- main.jsx
|       |-- App.jsx
|       |-- index.css
|       |-- App.css
|       |-- i18n/
|       |-- components/
|       |-- views/
|       |-- hooks/
|       |-- utils/
|       |-- context/
```

## Core Design Decisions

### 1. Telemetry Is Gathered On Request, But Sampling State Lives Longer Than A Request
- CPU, disk, and network rate samplers require prior samples.
- Network throughput also requires previous byte counters.
- The daemon must keep a singleton telemetry sampler alive for the process lifetime, warm it on startup, and produce a fresh mapped snapshot per API request.
- Do not instantiate platform samplers inside each request handler.

### 2. RigMonitor Owns The Public Contract
- The API returns only RigMonitor DTOs.
- No platform-specific internal sampler types or raw `TcpConnectionInformation` objects are allowed in public request or response contracts.
- GPU DTOs must be nullable or omitted when unsupported so the API remains stable when no GPU provider is active.

### 3. GPU Support Must Be Vendor-Extensible
- Introduce `IGpuTelemetryProvider` now.
- Implement `NvidiaDcgmGpuTelemetryProvider` first.
- Put vendor-neutral fields in shared DTOs: `Vendor`, `Model`, `Uuid`, `BusId`, `DriverVersion`, `DeviceIndex`, `MigProfile`, `Metrics`.
- Avoid naming top-level models or services in NVIDIA-specific terms.

### 4. Dashboard Must Be Same-Port And Optional
- API and dashboard must be hosted by the same Watson 7 process.
- When `Dashboard.Enabled = true`, `/` should redirect to `/dashboard`.
- When disabled, `/dashboard` and static asset routes should return `404`.
- The server must serve the built React app, its assets, and an SPA fallback route using a `StaticFileHandler` pattern similar to the reference Watson 7 services.

### 5. Internal Telemetry Logic Must Be Separated By Project Boundary
- Telemetry collection belongs in `RigMonitor.Telemetry`, not in the Watson server project.
- `RigMonitor.Telemetry` may reuse, port, or simplify logic from `PerformanceStatistics`, but it should not preserve `PerformanceStatistics` as a public compatibility layer.
- The code should be organized around RigMonitor's needs first:
  - full snapshot aggregation
  - platform-aware memory collection
  - network throughput sampling
  - disk inventory and rates
  - GPU exporter scraping
- If code is copied or adapted from `PerformanceStatistics`, normalize naming and APIs to RigMonitor conventions rather than carrying over a general-purpose library surface.

## Settings Model

### Root Settings Object
- `RigMonitorSettings`
  - `Webserver`
  - `Telemetry`
  - `Dashboard`
  - `Logging`

### Required Settings Areas
- `Webserver.Hostname`
  - Default `localhost`
- `Webserver.Port`
  - Default `9000`
- `Webserver.Ssl`
  - Default `false`
- `Webserver.Cors`
  - Mirror the `CorsSettings` pattern used in the reference projects
- `Logging`
  - Mirror the existing `LoggingSettings` conventions:
  - `Servers`
  - `LogDirectory`
  - `LogFilename`
  - `FileLogging`
  - `IncludeDateInFilename`
  - `ConsoleLogging`
  - `EnableColors`
  - `MinimumSeverity`

### Additional Settings To Add
- `Telemetry.DcgmExporterUrl`
  - Default `http://localhost:9400/metrics`
- `Telemetry.OllamaBaseUrl`
  - Default `http://localhost:11434`
- `Telemetry.RequestTimeoutMs`
  - Controls outbound scrape timeout
- `Telemetry.WarmupDelayMs`
  - Short delay used to seed CPU and rate samplers during startup
- `Dashboard.Enabled`
  - Default `true`
- `Dashboard.Title`
  - Human-readable title for the UI
- `Dashboard.AutoRefreshIntervalMs`
  - Default polling interval for the UI

## Telemetry Contract

### Root Snapshot
- `TelemetrySnapshot`
  - `CollectedUtc`
  - `HostPlatform`
  - `NvidiaAvailable`
  - `OllamaAvailable`
  - `System`
  - `Cpu`
  - `Memory`
  - `Network`
  - `Disk`
  - `Gpu` nullable
  - `Ollama` nullable

### System Section
- `SystemTelemetry`
  - `Hostname`
  - `UptimeMs`
  - `OsDescription`
  - `OsArchitecture`
  - `ProcessArchitecture`

Implementation notes:
- `Hostname` via `Environment.MachineName`
- `UptimeMs` via `Environment.TickCount64`

### CPU Section
- `CpuTelemetry`
  - `LogicalCoreCount`
  - `UtilizationPercent`
  - Optional process-level values if useful to expose in v1

Implementation notes:
- Implement CPU sampling internally with persistent cross-platform samplers
- Warm sampler before first API request

### Memory Section
- `MemoryTelemetry`
  - `TotalBytes`
  - `AvailableBytes`
  - `UsedBytes`
  - `UtilizationPercent`

Implementation notes:
- Implement a first-party memory provider that gathers total and available memory cross-platform.
- Do not preserve a partial "free-memory only" contract from legacy code.

### Network Section
- `NetworkTelemetry`
  - `TotalReceiveBytesPerSecond`
  - `TotalTransmitBytesPerSecond`
  - `ActiveInterfaceCount`
  - `Interfaces`
- `NetworkInterfaceTelemetry`
  - `Name`
  - `Description`
  - `Type`
  - `OperationalStatus`
  - `MacAddress`
  - `UnicastAddresses`
  - `ReceiveBytesPerSecond`
  - `TransmitBytesPerSecond`
  - `BytesReceivedTotal`
  - `BytesSentTotal`

Implementation notes:
- Network throughput must be a first-party sampler because RigMonitor needs interface-level and aggregate rates in a specific shape.
- Use `NetworkInterface.GetAllNetworkInterfaces()` and track deltas over time.

### Disk Section
- `DiskTelemetry`
  - `ReadOperationsPerSecond`
  - `WriteOperationsPerSecond`
  - `ReadQueueDepth`
  - `WriteQueueDepth`
  - `Volumes`
- `DiskVolumeTelemetry`
  - `Name`
  - `MountPoint`
  - `DriveType`
  - `FileSystem`
  - `TotalBytes`
  - `FreeBytes`
  - `UsedBytes`
  - `UtilizationPercent`

Implementation notes:
- Implement aggregate disk counters internally using platform-specific samplers.
- Use `DriveInfo` for per-volume manifest and capacity values.

### GPU Section
- `GpuTelemetry`
  - `Vendor`
  - `ExporterEndpoint`
  - `Devices`
- `GpuDeviceTelemetry`
  - `DeviceIndex`
  - `Uuid`
  - `BusId`
  - `Model`
  - `DriverVersion`
  - `MigProfile`
  - `Metrics`
- `GpuUtilizationTelemetry`
  - `GpuUtilizationPercent`
  - `MemoryUsedMegabytes`
  - `MemoryFreeMegabytes`
  - `TemperatureCelsius`
  - `PowerUsageWatts`
  - `SmClockMHz`
  - `MemoryClockMHz`
  - `XidErrors`

Implementation notes:
- Parse the Prometheus exposition from DCGM exporter.
- Map only the metrics RigMonitor owns.
- Start with the following DCGM metric families where present:
  - `DCGM_FI_DEV_GPU_UTIL`
  - `DCGM_FI_DEV_FB_USED`
  - `DCGM_FI_DEV_FB_FREE`
  - `DCGM_FI_DEV_GPU_TEMP`
  - `DCGM_FI_DEV_POWER_USAGE`
  - `DCGM_FI_DEV_SM_CLOCK`
  - `DCGM_FI_DEV_MEM_CLOCK`
  - `DCGM_FI_DEV_XID_ERRORS`
- Preserve MIG-related labels when present instead of collapsing them away.

### Ollama Section
- `OllamaTelemetry`
  - `Available`
  - `BaseUrl`
  - `Version`
  - `AvailableModelCount`
  - `LoadedModelCount`
  - `AvailableModels`
  - `LoadedModels`
- `OllamaModelTelemetry`
  - `Name`
  - `Model`
  - `Digest`
  - `SizeBytes`
  - `ModifiedUtc`
  - `Family`
  - `Format`
  - `ParameterSize`
  - `QuantizationLevel`
- `OllamaLoadedModelTelemetry`
  - `Name`
  - `Model`
  - `Digest`
  - `ExpiresAtUtc`
  - `SizeBytes`
  - `SizeVramBytes`
  - `Family`
  - `Format`
  - `ParameterSize`
  - `QuantizationLevel`

Implementation notes:
- On startup, probe Ollama availability similarly to the DCGM probe and keep the result in runtime capabilities.
- When Ollama is available, telemetry collection must include a distinct `Ollama` section with subobjects for available models and currently loaded models.

## API Surface
- `GET /livez`
  - Returns process liveness
- `GET /readyz`
  - Returns startup readiness including sampler warmup completion
- `GET /v1/capabilities`
  - Returns runtime capabilities including `NvidiaAvailable`, platform, and dashboard status
- `GET /v1/telemetry`
  - Gathers and returns a full `TelemetrySnapshot`
- `GET /openapi`
  - Watson 7 OpenAPI UI if enabled by the server
- `GET /openapi.json`
  - OpenAPI document
- `GET /dashboard/*`
  - Static dashboard routes when enabled

## Implementation Phases

### Phase 0: Repository Bootstrap
- [ ] Initialize the repo structure under `C:\code\rigmonitor`.
- [ ] Add `.gitignore`.
- [ ] Add `.dockerignore`.
- [ ] Add `README.md`.
- [ ] Add `CHANGELOG.md`.
- [ ] Add `LICENSE.md` with MIT text.
- [ ] Add `rigmonitor.json` seed file with defaults.
- [ ] Add `src/RigMonitor.sln`.

Acceptance criteria:
- The repository matches the structural requirements from `C:\code\claude\REPOSITORY_REQUIREMENTS.md`.

### Phase 1: Core Projects And Settings
- [ ] Create `RigMonitor.Core`.
- [ ] Create `RigMonitor.Telemetry`.
- [ ] Create `RigMonitor.Server`.
- [ ] Set `Nullable` to `enable` and `ImplicitUsings` to `disable`.
- [ ] Add `Watson` and `SyslogLogging` package references.
- [ ] Do not add a `PerformanceStatistics` NuGet dependency.
- [ ] Port the relevant cross-platform sampling logic into `RigMonitor.Telemetry`.
- [ ] Create owned settings classes.
- [ ] Implement first-run settings file creation and loading.
- [ ] Mirror the reference logging initialization flow:
  - syslog server mapping
  - console logging
  - file logging
  - minimum severity
  - log directory creation

Acceptance criteria:
- The daemon can start with a generated `rigmonitor.json`.
- Logging works to console and file according to settings.

### Phase 2: Watson 7 Host Composition
- [ ] Create `RigMonitorServer.cs` as the orchestration host.
- [ ] Keep `Program.cs` thin.
- [ ] Register `Preflight` and `PostRouting` handlers for CORS.
- [ ] Enable Watson health check and OpenAPI support.
- [ ] Create feature-specific route registrar classes rather than a monolithic handler file.
- [ ] Add graceful shutdown with `CancellationTokenSource`.

Acceptance criteria:
- The service starts cleanly, responds to `OPTIONS`, and emits OpenAPI.

### Phase 3: Telemetry Domain Contracts And System Provider
- [ ] Create all RigMonitor DTOs and keep one class per file.
- [ ] Create `ITelemetryService`.
- [ ] Create `ISystemTelemetryProvider`.
- [ ] Implement singleton platform-specific telemetry providers in `RigMonitor.Telemetry`.
- [ ] Port and adapt the relevant CPU, memory, disk, and process-sampling logic from `PerformanceStatistics`.
- [ ] Warm CPU and disk counters during startup.
- [ ] Implement a cross-platform memory info provider for total-memory values.
- [ ] Implement a cross-platform network rate sampler using interface byte counters.
- [ ] Implement disk volume mapping using `DriveInfo`.
- [ ] Ensure the telemetry project exposes only RigMonitor interfaces and DTOs.

Acceptance criteria:
- `ITelemetryService.GetSnapshotAsync` can build a complete non-GPU snapshot on all supported platforms.

### Phase 4: Runtime Capabilities And DCGM Detection
- [ ] Create `RuntimeCapabilitiesService`.
- [ ] Add a startup probe that scrapes the configured DCGM exporter URL.
- [ ] Set global `NvidiaAvailable` based on probe success and presence of DCGM device metrics.
- [ ] Add a startup probe for the configured Ollama base URL.
- [ ] Set global `OllamaAvailable` based on probe success.
- [ ] Expose runtime capabilities through `GET /v1/capabilities`.
- [ ] Make detection failures non-fatal and log them clearly.

Acceptance criteria:
- The daemon starts even if DCGM is absent.
- `NvidiaAvailable` is deterministic and externally visible.

### Phase 5: NVIDIA GPU Provider
- [ ] Create `IDcgmExporterClient`.
- [ ] Implement `DcgmExporterClient` with timeout and cancellation support.
- [ ] Parse Prometheus text exposition into an internal representation.
- [ ] Implement `NvidiaDcgmGpuTelemetryProvider`.
- [ ] Map DCGM labels and values into RigMonitor GPU DTOs.
- [ ] Only include `Gpu` in the snapshot when `NvidiaAvailable` is `true` and the scrape succeeds.
- [ ] Handle partial metric availability without failing the entire snapshot.

Acceptance criteria:
- GPU telemetry is returned for NVIDIA hosts with DCGM exporter.
- GPU telemetry is omitted, not malformed, when DCGM is unavailable.

### Phase 6: Telemetry API
- [ ] Implement `GET /livez`.
- [ ] Implement `GET /readyz`.
- [ ] Implement `GET /v1/capabilities`.
- [ ] Implement `GET /v1/telemetry`.
- [ ] Configure JSON serialization to omit null values where appropriate so `Gpu` disappears when unavailable.
- [ ] Return typed responses only.
- [ ] Log request completion and failures with context.

Acceptance criteria:
- The API surface is discoverable through OpenAPI and returns stable JSON contracts.

### Phase 7: Dashboard
- [ ] Scaffold the React dashboard in `dashboard/`.
- [ ] Use React 19, Vite 6, React Router 7, browser `fetch`, and `i18next`.
- [ ] Use `assets/icon.png` in the dashboard chrome and `assets/icon.ico` as the favicon.
- [ ] Add `src/i18n/` with locale bootstrap, registry, resources, and formatters.
- [ ] Build a same-port operator dashboard with at least:
  - Home overview
  - System section
  - CPU section
  - Memory section
  - Network section
  - Disk section
  - GPU section when available
  - API Explorer view if the team wants parity with the reference Watson dashboards
- [ ] Add loading, empty, and error states for every data-bearing view.
- [ ] Add a manual refresh action and optional auto-refresh toggle.
- [ ] Keep the layout responsive for desktop and mobile widths.
- [ ] Do not add a charting library.
- [ ] Serve the built dashboard from the Watson process using a static file handler plus SPA fallback.
- [ ] Hide or suppress GPU UI panels when GPU telemetry is unavailable, while still showing a small capability indicator if useful.

Acceptance criteria:
- When `Dashboard.Enabled = true`, the root URL lands on the dashboard and the UI can render the current telemetry snapshot from the same origin.
- When `Dashboard.Enabled = false`, dashboard routes are not served.

### Phase 8: Docker And Container Operations
- [ ] Add `docker/Dockerfile` as a multi-stage build:
  - build server
  - build dashboard
  - copy dashboard output into the server runtime image
- [ ] Add `docker/compose.yaml` using `build.context`, not a named registry image.
- [ ] Persist settings and logs using a bind-mounted `docker/data/` directory.
- [ ] Run the daemon with a settings path under the persisted data directory.
- [ ] Make the DCGM exporter URL configurable so container deployments can target:
  - `host.docker.internal`
  - another compose service
  - localhost on bare metal
- [ ] Consider an optional compose profile or commented example for a sidecar `dcgm-exporter` service on Linux NVIDIA hosts.

Acceptance criteria:
- Container restarts preserve settings and logs.
- The dashboard and API are available through the configured port.

### Phase 9: Testing
- [ ] Create `Test.Shared` with Touchstone descriptors.
- [ ] Create `Test.Automated`.
- [ ] Create `Test.Xunit`.
- [ ] Create `Test.Nunit`.
- [ ] Add unit and integration coverage for:
  - settings validation
  - logging initialization
  - CORS headers
  - startup settings generation
  - telemetry DTO mapping
  - CPU warmup behavior
  - memory provider behavior
  - network rate sampling
  - disk volume mapping
  - DCGM scrape success
  - DCGM scrape failure
  - GPU omission when unavailable
  - API route responses
  - dashboard enabled vs disabled routing
- [ ] Add frontend smoke coverage for:
  - loading state
  - error state
  - GPU section present/absent behavior
  - locale switch and persistence
  - formatting helpers for bytes, percentages, and durations

Acceptance criteria:
- Automated test suites exist and can be run without manual setup beyond platform prerequisites.

### Phase 10: Documentation And Finish Work
- [ ] Write `README.md` with:
  - product purpose
  - supported platforms
  - settings file reference
  - API routes
  - dashboard usage
  - Docker usage
  - DCGM setup notes
- [ ] Update `CHANGELOG.md`.
- [ ] Document the NVIDIA-only v1 scope and the provider-extension path for AMD or Intel.
- [ ] Document how to add a future `IGpuTelemetryProvider` implementation.
- [ ] Document how to run tests locally.

Acceptance criteria:
- A new developer can clone the repo, start the daemon, call the API, and understand the extension model from the docs.

## UX Checklist
- [ ] All user-visible strings must come from the i18n layer.
- [ ] Localize page titles, labels, tooltips, empty states, loading states, and error messages.
- [ ] Add a language selector to the dashboard chrome.
- [ ] Route bytes, percentages, and durations through shared formatters.
- [ ] Make tables and cards resilient to long labels and value overflow.
- [ ] Keep the GPU section absent when unsupported rather than showing broken placeholders.
- [ ] Keep the Ollama section absent when unsupported rather than showing broken placeholders.
- [ ] Ensure keyboard accessibility for refresh controls and navigation.

## Definition Of Done
- [ ] Repo structure, license, and housekeeping files exist.
- [ ] The daemon runs on Linux, Windows, and macOS.
- [ ] Settings file creation and loading work.
- [ ] Logging, CORS, Watson health check, and OpenAPI are wired correctly.
- [ ] `GET /v1/telemetry` returns a fully mapped RigMonitor DTO graph.
- [ ] GPU telemetry appears only when DCGM exporter is available.
- [ ] Dashboard is served on the same port when enabled.
- [ ] Dashboard is hidden when disabled.
- [ ] Docker compose uses a build context and persists settings/logs.
- [ ] Tests exist across the required layers.
- [ ] README and changelog are complete.

## Risks And Notes
- Folding telemetry logic into this repo improves control, but it also makes RigMonitor fully responsible for cross-platform sampler correctness and regressions.
- CPU and rate-based counters need warmup samples; skipping startup warmup will produce misleading zeros.
- Containerized GPU telemetry depends on reachable DCGM exporter placement; treat that as deployment configuration, not hard-coded behavior.
- Keep the public contract vendor-neutral from day one so future GPU vendors do not require a breaking API revision.
