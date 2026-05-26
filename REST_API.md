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
- Optional sections are omitted from telemetry responses when not requested or when no current sample is available.
- `/v1/telemetry` includes a top-level `collection` object so clients can distinguish intentionally omitted data from unhealthy or unsupported sections.

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
- `collection`
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
  "collection": {
    "collectedUtc": "2026-05-18T14:12:13.6414917Z",
    "staleAfterMs": 15000,
    "system": {
      "requested": true,
      "supported": true,
      "statusCode": "ok",
      "lastAttemptUtc": "2026-05-18T14:12:13.6414917Z",
      "lastSuccessUtc": "2026-05-18T14:12:13.6414917Z",
      "lastDurationMs": 3.2,
      "freshness": {
        "status": "fresh",
        "ageMs": 0,
        "staleAfterMs": 15000
      },
      "message": "System telemetry collected successfully."
    },
    "gpu": {
      "requested": true,
      "supported": false,
      "statusCode": "unsupported",
      "freshness": {
        "status": "notApplicable",
        "staleAfterMs": 15000
      },
      "message": "GPU telemetry is unsupported on this host."
    }
  },
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

#### Collection metadata

`collection` does not wrap the existing telemetry payload. It is additional metadata keyed by section name:

- `collection.system`
- `collection.cpu`
- `collection.memory`
- `collection.network`
- `collection.disk`
- `collection.gpu`
- `collection.ollama`

Each section status object contains:

- `requested`: whether the section was requested for this snapshot
- `supported`: whether the host supports collecting the section
- `statusCode`: one of `ok`, `disabled`, `unsupported`, `unavailable`, `error`, or `stale`
- `lastAttemptUtc`: time of the most recent collection attempt
- `lastSuccessUtc`: time of the most recent successful collection attempt
- `lastDurationMs`: duration in milliseconds of the most recent collection attempt
- `freshness`: freshness evaluation for the most recent successful sample
- `message`: human-readable explanation of the current section state
- `lastError`: most recent collector error when one exists

`freshness` contains:

- `status`: one of `fresh`, `stale`, `unknown`, or `notApplicable`
- `ageMs`: age in milliseconds of the last successful sample when one exists
- `staleAfterMs`: the configured stale threshold

Section-state matrix:

| Situation | Data section | `statusCode` | `freshness.status` |
|-----------|--------------|--------------|--------------------|
| Section not requested | omitted | `disabled` | `notApplicable` |
| Section unsupported on this host | omitted | `unsupported` | `notApplicable` |
| Section supported but no current sample exists | omitted | `unavailable` | `unknown` or `fresh` |
| Collector threw and no stale last success exists | omitted | `error` | `unknown` or `fresh` |
| Latest success aged past `Telemetry.SectionStaleAfterMs` and a current request did not succeed | omitted | `stale` | `stale` |
| Current request succeeded | present | `ok` | `fresh` |

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

Even when a section is omitted by selector, its collection metadata remains present. Example:

```json
{
  "collection": {
    "gpu": {
      "requested": false,
      "supported": true,
      "statusCode": "disabled",
      "freshness": {
        "status": "notApplicable",
        "staleAfterMs": 15000
      },
      "message": "GPU telemetry was intentionally not requested."
    }
  }
}
```

#### Optional sections

- `gpu` is present only when NVIDIA telemetry is supported, requested, and a current sample succeeds.
- `ollama` is present only when Ollama is supported, requested, and a current sample succeeds.

Examples of omitted-vs-unhealthy states:

Unsupported GPU section:

```json
{
  "collection": {
    "gpu": {
      "requested": true,
      "supported": false,
      "statusCode": "unsupported",
      "freshness": {
        "status": "notApplicable",
        "staleAfterMs": 15000
      },
      "message": "GPU telemetry is unsupported on this host."
    }
  }
}
```

Temporarily unavailable GPU section:

```json
{
  "collection": {
    "gpu": {
      "requested": true,
      "supported": true,
      "statusCode": "unavailable",
      "lastAttemptUtc": "2026-05-18T14:16:03.1000000Z",
      "lastDurationMs": 22.4,
      "freshness": {
        "status": "unknown",
        "staleAfterMs": 15000
      },
      "message": "GPU telemetry is temporarily unavailable and no successful sample has been recorded yet."
    }
  }
}
```

Stale Ollama section after a previous success:

```json
{
  "collection": {
    "ollama": {
      "requested": true,
      "supported": true,
      "statusCode": "stale",
      "lastAttemptUtc": "2026-05-18T14:18:03.1000000Z",
      "lastSuccessUtc": "2026-05-18T14:17:40.0000000Z",
      "lastDurationMs": 105.6,
      "freshness": {
        "status": "stale",
        "ageMs": 23100,
        "staleAfterMs": 15000
      },
      "message": "Ollama telemetry is stale because the most recent successful sample is older than the freshness window.",
      "lastError": "Collection timed out before the section returned a sample."
    }
  }
}
```

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
