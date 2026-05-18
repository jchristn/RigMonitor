# RigMonitor REST API

## Base URL

By default the daemon listens on:

```text
http://127.0.0.1:9990
```

If you change `Webserver.Hostname`, `Webserver.Port`, or `Webserver.Ssl` in `rigmonitor.json`, adjust the base URL accordingly.

## Response Conventions

- Response property names are camelCase.
- RigMonitor-owned classification values are emitted as camelCase strings.
- Timestamps are UTC ISO 8601 strings.
- Optional sections are omitted from telemetry responses when not requested or not available.

Examples of normalized values:

- `hostPlatform: "windows"`
- `osArchitecture: "x64"`
- `type: "wireless80211"`
- `operationalStatus: "up"`
- `driveType: "fixed"`

Upstream opaque strings from external systems are preserved as provided. For example, Ollama may return values such as `Q4_K_M`.

## Endpoints

### `GET /livez`

Liveness probe for the daemon.

- `200 OK` when the process is up and listening.

### `GET /readyz`

Readiness probe for telemetry collection.

- `200 OK` when telemetry warmup has completed.
- `503 Service Unavailable` while telemetry is still warming.

Example:

```json
{
  "status": "ready",
  "ready": true,
  "message": "Telemetry samplers are warm.",
  "timestampUtc": "2026-05-18T14:07:34.6994969Z"
}
```

### `GET /v1/capabilities`

Returns runtime capability flags and startup probe results.

Example:

```json
{
  "collectedUtc": "2026-05-18T14:07:34.6994969Z",
  "hostPlatform": "windows",
  "dashboardEnabled": true,
  "telemetryWarm": true,
  "nvidiaAvailable": false,
  "ollamaAvailable": true,
  "dcgmExporterUrl": "http://localhost:9400/metrics",
  "ollamaBaseUrl": "http://localhost:11434"
}
```

### `GET /v1/telemetry`

Returns a host telemetry snapshot. By default all supported sections are included.

Top-level fields:

- `collectedUtc`
- `hostPlatform`
- `nvidiaAvailable`
- `ollamaAvailable`
- `system`
- `cpu`
- `memory`
- `network`
- `disk`
- `gpu`
- `ollama`

Example:

```json
{
  "collectedUtc": "2026-05-18T14:12:13.6414917Z",
  "hostPlatform": "windows",
  "nvidiaAvailable": false,
  "ollamaAvailable": true,
  "system": {
    "hostname": "THINKPAD",
    "uptimeMs": 135119109,
    "osDescription": "Microsoft Windows 10.0.26200",
    "osArchitecture": "x64",
    "processArchitecture": "x64"
  },
  "cpu": {
    "logicalCoreCount": 24,
    "utilizationPercent": 18.498476028442383
  },
  "memory": {
    "totalBytes": 98403270656,
    "availableBytes": 53928210432,
    "usedBytes": 44475060224,
    "utilizationPercent": 45.196729669155765
  }
}
```

#### Selective telemetry query parameters

Recognized selector keys:

- `system`
- `cpu`
- `memory`
- `network`
- `disk`
- `gpu`
- `ollama`

Rules:

- If no recognized selector keys are present, all sections are included by default.
- Presence of a key means `true`.
- A key set to `=false` means `false`.
- Once one or more recognized selector keys are present, unspecified recognized sections are treated as `false`.

Example request:

```text
GET /v1/telemetry?cpu&memory&network&gpu=false
```

Behavior:

- includes `cpu`
- includes `memory`
- includes `network`
- excludes `gpu`
- omits `system`
- omits `disk`
- omits `ollama`

#### Optional sections

- `gpu` is present only when NVIDIA telemetry is available and requested.
- `ollama` is present only when Ollama is reachable and requested.

The `ollama` object contains:

- `available`
- `baseUrl`
- `version`
- `collectedUtc`
- `availableModelCount`
- `loadedModelCount`
- `availableModels`
- `loadedModels`

### `GET /openapi.json`

Returns the generated OpenAPI document for the daemon.

### `GET /openapi`

Returns Swagger UI for the generated OpenAPI document.

### `GET /dashboard`

Returns the dashboard single-page application when `Dashboard.Enabled` is `true`.

### `GET /favicon.ico`

Returns the dashboard favicon sourced from `assets/icon.ico` when the dashboard is enabled.
