# RigMonitor

<img src="assets/icon.png" alt="RigMonitor icon" width="192" height="192" />

Version `0.1.0`

Note: RigMonitor is currently in `ALPHA`. Expect API, dashboard, configuration, and telemetry surface changes while the project is still stabilizing.

RigMonitor is a cross-platform telemetry daemon for Windows, Linux, and macOS. It serves Watson 7 API endpoints for host telemetry and, when enabled, a same-port dashboard at `/dashboard`.

## Why Use RigMonitor

RigMonitor is useful when you operate a set of CPU- and GPU-powered systems and need a lightweight way to monitor them periodically with a consistent API and dashboard.

Typical use cases include:

- watching a fleet of workstations, inference rigs, and training nodes for operational health
- seeing which hosts currently expose GPU telemetry, model-runner availability, memory headroom, and network activity
- informing intelligent workload-placement decisions for AI inference
- supporting load-distribution choices across multiple AI-serving or AI-training systems

It is meant for environments where hardware visibility is not just operationally interesting, but directly useful for deciding where work should run.

Detailed endpoint and payload documentation lives in [REST_API.md](./REST_API.md).

NVIDIA GPU telemetry setup is documented in [INSTALLING_DCGM.md](./INSTALLING_DCGM.md).

## Features

- CPU, memory, network, and disk telemetry
- Optional NVIDIA GPU telemetry through DCGM exporter
- Optional Ollama telemetry with available models and loaded models
- Optional vLLM telemetry through its Prometheus metrics endpoint
- Optional Utilyze telemetry through a sidecar WebSocket service for GPU SOL, bandwidth, and attainable ceiling metrics
- Structured per-section collection metadata with request state, support state, freshness, last success, and stable status codes
- Same-port dashboard with manual refresh, auto-refresh, i18n, and vLLM/Utilyze status/telemetry cards
- OpenAPI document at `/openapi.json` and Swagger UI at `/openapi`

## Endpoints

- `GET /livez`
- `GET /readyz`
- `GET /v1/capabilities`
- `GET /v1/telemetry`
- `GET /openapi`
- `GET /openapi.json`
- `GET /dashboard`

## Selective Telemetry

`GET /v1/telemetry` accepts optional query keys to select which sections are collected:

- `system`
- `cpu`
- `memory`
- `network`
- `disk`
- `gpu`
- `ollama`
- `vllm`
- `utilyze`

Rules:

- When no recognized selector keys are present, all sections are included by default.
- Presence of a key means `true`.
- A key set to `=false` means `false`.
- Once one or more recognized selector keys are present, unspecified recognized sections are omitted.

Example:

```text
http://127.0.0.1:9990/v1/telemetry?cpu&memory&network&gpu=false
```

All API response property names are camelCase. RigMonitor-owned classification values are also emitted as camelCase strings, for example `windows`, `x64`, `wireless80211`, `up`, and `fixed`. Upstream opaque strings such as Ollama quantization labels are passed through as provided.

Every `/v1/telemetry` response now also includes a top-level `collection` object. It does not wrap or rename the existing `system`, `cpu`, `memory`, `network`, `disk`, `gpu`, `ollama`, `vllm`, or `utilyze` payload sections. Instead, it explains why a section is present, omitted, unsupported, temporarily unavailable, errored, or stale.

## Section States

`collection.<section>` reports:

- `requested`
- `supported`
- `statusCode`
- `lastAttemptUtc`
- `lastSuccessUtc`
- `lastDurationMs`
- `freshness`
- `message`
- `lastError`

Status matrix:

| Case | Data section | `requested` | `supported` | `statusCode` |
|------|--------------|-------------|-------------|--------------|
| Query omitted the section | omitted | `false` | host-dependent | `disabled` |
| Host cannot provide the section | omitted | `true` | `false` | `unsupported` |
| Collector returned no current sample | omitted | `true` | `true` | `unavailable` |
| Collector threw and no stale success exists | omitted | `true` | `true` | `error` |
| Collector failed and the last success aged past the stale window | omitted | `true` | `true` | `stale` |
| Collector succeeded | present | `true` | `true` | `ok` |

The `freshness` object is evaluated from the last successful sample. This means a section can be currently unavailable while still showing when it last succeeded and whether that success is still inside the configured stale window.

## Local Run

Run these commands from the repository root to run RigMonitor outside of Docker.

1. Build the dashboard bundle:

   ```powershell
   cd dashboard
   npm install
   npm run build
   ```

2. Build the .NET solution. This copies the existing `dashboard/dist` bundle into the server output under `wwwroot`.

   ```powershell
   cd ..\src
   dotnet build
   ```

3. Run the server from the build output:

   ```powershell
   cd RigMonitor.Server\bin\Debug\net10.0
   .\RigMonitor.Server.exe
   ```

   The executable uses `rigmonitor.json` from the current directory by default, creating one if it does not exist. To use the repository-root settings file instead, pass `--settings ..\..\..\..\..\rigmonitor.json`.

4. Open:

- Dashboard: `http://localhost:9990/dashboard`
- OpenAPI UI: `http://localhost:9990/openapi`

## Settings

The default settings file is `rigmonitor.json`. The daemon creates it automatically when missing.

Relevant telemetry settings:

- `Telemetry.DcgmExporterUrl`
- `Telemetry.OllamaBaseUrl`
- `Telemetry.VllmEnabled`
- `Telemetry.VllmMetricsUrl`
- `Telemetry.UtilyzeEnabled`
- `Telemetry.UtilyzeLiveUrl`
- `Telemetry.UtilyzeClientId`
- `Telemetry.UtilyzeSampleStaleAfterMs`
- `Telemetry.RequestTimeoutMs`
- `Telemetry.WarmupDelayMs`
- `Telemetry.SectionStaleAfterMs`

Dashboard settings:

- `Dashboard.Enabled`
- `Dashboard.Title`
- `Dashboard.AutoRefreshIntervalMs`

## NVIDIA GPU Telemetry

RigMonitor collects NVIDIA GPU telemetry through NVIDIA DCGM exporter, not directly from `nv-hostengine`.

The default endpoint is:

```text
http://localhost:9400/metrics
```

For GPU telemetry to appear:

- NVIDIA drivers and DCGM must be installed.
- `nv-hostengine` can be running through `nvidia-dcgm.service`, commonly on port `5555`.
- DCGM exporter must also be installed and running, commonly as `nvidia-dcgm-exporter.service`.
- `curl http://127.0.0.1:9400/metrics` must return Prometheus metrics containing names such as `DCGM_FI_DEV_GPU_UTIL`, `DCGM_FI_DEV_FB_USED`, `DCGM_FI_DEV_FB_FREE`, or `DCGM_FI_DEV_GPU_TEMP`.
- RigMonitor must be started after the exporter is reachable, or restarted after installing or fixing the exporter.

On GB10 unified-memory systems, DCGM may report zero framebuffer memory because GPU memory is shared with system RAM. RigMonitor falls back to host physical memory for GPU RAM display on those systems and marks the GPU memory source as shared.

See [INSTALLING_DCGM.md](./INSTALLING_DCGM.md) for Ubuntu install, verification, and troubleshooting commands.

## Docker

Use the bundled compose file:

```powershell
cd docker
docker compose up --build
```

This persists settings and logs under `docker/data/`.

## GPU, Ollama, vLLM, And Utilyze Notes

- NVIDIA telemetry is available only when the configured DCGM exporter is reachable at startup. DCGM hostengine alone is not enough.
- If DCGM exporter is installed or started after RigMonitor, restart RigMonitor so `/v1/capabilities` can refresh `nvidiaAvailable`.
- Ollama telemetry is available only when the configured Ollama API is reachable at startup.
- vLLM telemetry is available only when `Telemetry.VllmEnabled` is `true` and the configured Prometheus metrics endpoint is reachable at startup.
- Utilyze telemetry is available only when `Telemetry.UtilyzeEnabled` is `true` and a Utilyze sidecar is reachable at `Telemetry.UtilyzeLiveUrl` during startup. RigMonitor consumes Utilyze over its live WebSocket API and does not launch or supervise `utlz`.
- Utilyze collection requires NVIDIA profiling permissions on the collecting Linux host. Configure the Utilyze process separately, including `UTLZ_DISABLE_METRICS=1` if you do not want Utilyze to send aggregate roofline data to Systalyze.
- The dashboard keeps optional telemetry cards visible and uses `collection` metadata to explain disabled, unsupported, unavailable, error, and stale states.
