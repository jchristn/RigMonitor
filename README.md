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

## Features

- CPU, memory, network, and disk telemetry
- Optional NVIDIA GPU telemetry through DCGM exporter
- Optional Ollama telemetry with available models and loaded models
- Structured per-section collection metadata with request state, support state, freshness, last success, and stable status codes
- Same-port dashboard with manual refresh, auto-refresh, and i18n
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

Every `/v1/telemetry` response now also includes a top-level `collection` object. It does not wrap or rename the existing `system`, `cpu`, `memory`, `network`, `disk`, `gpu`, or `ollama` payload sections. Instead, it explains why a section is present, omitted, unsupported, temporarily unavailable, errored, or stale.

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

1. Build the dashboard:

   ```powershell
   cd dashboard
   npm.cmd install
   npm.cmd run build
   ```

2. Run the daemon:

   ```powershell
   dotnet run --project src/RigMonitor.Server/RigMonitor.Server.csproj -- --settings rigmonitor.json
   ```

3. Open:

- Dashboard: `http://localhost:9990/dashboard`
- OpenAPI UI: `http://localhost:9990/openapi`

## Settings

The default settings file is `rigmonitor.json`. The daemon creates it automatically when missing.

Relevant telemetry settings:

- `Telemetry.DcgmExporterUrl`
- `Telemetry.OllamaBaseUrl`
- `Telemetry.RequestTimeoutMs`
- `Telemetry.WarmupDelayMs`
- `Telemetry.SectionStaleAfterMs`

Dashboard settings:

- `Dashboard.Enabled`
- `Dashboard.Title`
- `Dashboard.AutoRefreshIntervalMs`

## Docker

Use the bundled compose file:

```powershell
cd docker
docker compose up --build
```

This persists settings and logs under `docker/data/`.

## GPU And Ollama Notes

- NVIDIA telemetry is available only when the configured DCGM exporter is reachable at startup.
- Ollama telemetry is available only when the configured Ollama API is reachable at startup.
- The dashboard keeps GPU and Ollama cards visible and uses `collection` metadata to explain disabled, unsupported, unavailable, error, and stale states.
