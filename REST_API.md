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
  "vllmEnabled": false,
  "vllmAvailable": false,
  "utilyzeEnabled": false,
  "utilyzeAvailable": false,
  "dcgmExporterUrl": "http://localhost:9400/metrics",
  "ollamaBaseUrl": "http://localhost:11434",
  "vllmMetricsUrl": "http://localhost:8000/metrics",
  "utilyzeLiveUrl": "ws://127.0.0.1:8079/live"
}
```

### `GET /v1/telemetry`

Returns a host telemetry snapshot. By default all supported sections are included.

Top-level fields:

- `collectedUtc`
- `hostPlatform`
- `nvidiaAvailable`
- `ollamaAvailable`
- `vllmAvailable`
- `utilyzeAvailable`
- `collection`
- `system`
- `cpu`
- `memory`
- `network`
- `disk`
- `gpu`
- `ollama`
- `vllm`
- `utilyze`

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
    },
    "vllm": {
      "requested": true,
      "supported": false,
      "statusCode": "unsupported",
      "freshness": {
        "status": "notApplicable",
        "staleAfterMs": 15000
      },
      "message": "vLLM telemetry is unsupported on this host."
    },
    "utilyze": {
      "requested": true,
      "supported": false,
      "statusCode": "unsupported",
      "freshness": {
        "status": "notApplicable",
        "staleAfterMs": 15000
      },
      "message": "Utilyze telemetry is unsupported on this host."
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
- `collection.vllm`
- `collection.utilyze`

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
- `vllm`
- `utilyze`

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
- omits `vllm`
- omits `utilyze`

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
- `vllm` is present only when vLLM telemetry is enabled, reachable, requested, and a current metrics scrape succeeds.
- `utilyze` is present only when Utilyze is enabled, reachable, requested, and has a fresh live sample.

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

The `gpu` object contains:

- `vendor`
- `exporterEndpoint`
- `devices`

Each `gpu.devices[]` object contains:

- `deviceIndex`
- `uuid`
- `busId`
- `model`
- `driverVersion`
- `migProfile`
- `metrics`

The `gpu.devices[].metrics` object contains:

- `gpuUtilizationPercent`
- `memoryUsedMegabytes`
- `memoryFreeMegabytes`
- `memoryTotalMegabytes`
- `memoryUtilizationPercent`
- `memorySource`
- `memoryShared`
- `temperatureCelsius`
- `powerUsageWatts`
- `smClockMHz`
- `memoryClockMHz`
- `xidErrors`

`memoryTotalMegabytes` is read from `DCGM_FI_DEV_FB_TOTAL` when present. If the exporter does not emit that metric, RigMonitor derives total framebuffer memory from `DCGM_FI_DEV_FB_USED + DCGM_FI_DEV_FB_FREE`.

On GB10 unified-memory systems where DCGM reports zero framebuffer memory, RigMonitor reports host physical memory in the GPU memory fields and sets `memorySource` to `unifiedSystemMemory` with `memoryShared` set to `true`.

The `ollama` object contains:

- `available`
- `baseUrl`
- `version`
- `collectedUtc`
- `availableModelCount`
- `loadedModelCount`
- `availableModels`
- `loadedModels`

The `vllm` object contains:

- `available`
- `metricsEndpoint`
- `collectedUtc`
- `modelNames`
- `summary`
- `metrics`

The `vllm.summary` object contains normalized values when the corresponding Prometheus samples are present:

- `runningRequests`
- `waitingRequests`
- `swappedRequests`
- `gpuCacheUsagePercent`
- `cpuCacheUsagePercent`
- `promptTokensTotal`
- `generationTokensTotal`
- `successfulRequestsTotal`

Each `vllm.metrics[]` object contains:

- `name`
- `labels`
- `value`

Example vLLM section:

```json
{
  "vllmAvailable": true,
  "collection": {
    "vllm": {
      "requested": true,
      "supported": true,
      "statusCode": "ok",
      "lastAttemptUtc": "2026-06-01T20:20:30.122Z",
      "lastSuccessUtc": "2026-06-01T20:20:30.122Z",
      "lastDurationMs": 12.3,
      "freshness": {
        "status": "fresh",
        "ageMs": 0,
        "staleAfterMs": 15000
      },
      "message": "vLLM telemetry collected successfully."
    }
  },
  "vllm": {
    "available": true,
    "metricsEndpoint": "http://localhost:8000/metrics",
    "collectedUtc": "2026-06-01T20:20:30.122Z",
    "modelNames": [
      "meta-llama/Llama-3.1-70B-Instruct"
    ],
    "summary": {
      "runningRequests": 2,
      "waitingRequests": 1,
      "gpuCacheUsagePercent": 63.5,
      "promptTokensTotal": 123456,
      "generationTokensTotal": 789012,
      "successfulRequestsTotal": 345
    },
    "metrics": [
      {
        "name": "vllm:num_requests_running",
        "labels": {
          "model_name": "meta-llama/Llama-3.1-70B-Instruct"
        },
        "value": 2
      }
    ]
  }
}
```

The `utilyze` object contains:

- `available`
- `endpoint`
- `collectedUtc`
- `deviceIds`
- `devices`

Each `utilyze.devices[]` object contains:

- `deviceIndex`
- `online`
- `computeSolPercent`
- `memorySolPercent`
- `smActivePercent`
- `nvmlUtilizationPercent`
- `pcieTransmitBytesPerSecond`
- `pcieReceiveBytesPerSecond`
- `nvlinkTransmitBytesPerSecond`
- `nvlinkReceiveBytesPerSecond`
- `modelName`
- `computeSolCeilingPercent`

Example Utilyze section:

```json
{
  "utilyzeAvailable": true,
  "collection": {
    "utilyze": {
      "requested": true,
      "supported": true,
      "statusCode": "ok",
      "lastAttemptUtc": "2026-06-01T20:15:30.122Z",
      "lastSuccessUtc": "2026-06-01T20:15:30.122Z",
      "lastDurationMs": 1.7,
      "freshness": {
        "status": "fresh",
        "ageMs": 311,
        "staleAfterMs": 15000
      },
      "message": "Utilyze telemetry collected successfully."
    }
  },
  "utilyze": {
    "available": true,
    "endpoint": "ws://127.0.0.1:8079/live",
    "collectedUtc": "2026-06-01T20:15:29.814Z",
    "deviceIds": [0],
    "devices": [
      {
        "deviceIndex": 0,
        "online": true,
        "computeSolPercent": 73.42,
        "memorySolPercent": 46.18,
        "smActivePercent": 91.35,
        "nvmlUtilizationPercent": 97,
        "pcieTransmitBytesPerSecond": 128450000,
        "pcieReceiveBytesPerSecond": 94210000,
        "nvlinkTransmitBytesPerSecond": 3180000000,
        "nvlinkReceiveBytesPerSecond": 2940000000,
        "modelName": "meta-llama/Llama-3.1-70B-Instruct",
        "computeSolCeilingPercent": 84
      }
    ]
  }
}
```

## Telemetry History

Telemetry history endpoints read and manage samples collected by the background persistence worker. The worker stores queryable scalar columns and the original `TelemetrySnapshot` JSON payload in SQLite by default.

### `GET /v1/telemetry/history/status`

Returns persistence worker and database status.

Example:

```json
{
  "enabled": true,
  "hostname": "localhost",
  "collectionIntervalMs": 60000,
  "retentionDays": 30,
  "databaseType": "Sqlite",
  "databaseFilename": "data/rigmonitor.telemetry.db",
  "lastAttemptUtc": "2026-06-11T12:00:00Z",
  "lastSuccessUtc": "2026-06-11T12:00:00Z",
  "nextCollectionUtc": "2026-06-11T12:01:00Z"
}
```

### `POST /v1/telemetry/history/enumerate`

Returns `EnumerationResult<TelemetrySampleRecord>` using the continuation-token pattern. Use this when clients need stable forward paging over a time range.

Request:

```json
{
  "maxResults": 100,
  "continuationToken": null,
  "ordering": "createdDescending",
  "hostnameFilter": "localhost",
  "startUtc": "2026-06-11T05:00:00Z",
  "endUtc": "2026-06-11T06:00:00Z"
}
```

Response:

```json
{
  "success": true,
  "maxResults": 100,
  "totalRecords": 2,
  "recordsRemaining": 0,
  "endOfResults": true,
  "totalMs": 4.2,
  "objects": [
    {
      "id": "tel_...",
      "hostname": "localhost",
      "collectedUtc": "2026-06-11T05:40:00Z",
      "persistedUtc": "2026-06-11T05:40:01Z",
      "hostPlatform": "windows",
      "cpuUtilizationPercent": 40,
      "memoryUtilizationPercent": 70,
      "gpuAverageUtilizationPercent": 50
    }
  ]
}
```

### `POST /v1/telemetry/history/search`

Returns a request-history style paged search result ordered by collection time descending.

Request:

```json
{
  "hostname": "localhost",
  "hostPlatform": "windows",
  "gpuUuid": "GPU-1",
  "gpuModel": "RTX",
  "nvidiaAvailable": true,
  "ollamaAvailable": true,
  "vllmAvailable": null,
  "utilyzeAvailable": null,
  "startUtc": "2026-06-11T05:00:00Z",
  "endUtc": "2026-06-11T06:00:00Z",
  "minCpuUtilizationPercent": 10,
  "maxCpuUtilizationPercent": 90,
  "minMemoryUtilizationPercent": 25,
  "maxMemoryUtilizationPercent": 95,
  "minGpuUtilizationPercent": 0,
  "maxGpuUtilizationPercent": 100,
  "minGpuTemperatureCelsius": 40,
  "maxGpuTemperatureCelsius": 90,
  "page": 1,
  "pageSize": 25
}
```

Response:

```json
{
  "data": [
    {
      "id": "tel_...",
      "hostname": "localhost",
      "collectedUtc": "2026-06-11T05:40:00Z",
      "hostPlatform": "windows",
      "cpuUtilizationPercent": 40,
      "memoryUtilizationPercent": 70,
      "gpuDeviceCount": 1
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1,
  "totalPages": 1
}
```

### `GET /v1/telemetry/history/{sampleId}`

Returns one persisted sample with all scalar fields and the original `snapshot` payload.

Responses:

- `200 OK` with `TelemetrySampleDetail`
- `404 Not Found` when the sample does not exist

### `POST /v1/telemetry/history/rollups`

Returns bucketized aggregate telemetry over a time range. `startUtc`, `endUtc`, and `bucketMinutes` are authoritative; `includeEmptyBuckets` should be `true` for charting.

Request:

```json
{
  "hostname": "localhost",
  "startUtc": "2026-06-11T05:00:00Z",
  "endUtc": "2026-06-11T06:00:00Z",
  "bucketMinutes": 60,
  "gpuUuid": null,
  "includeEmptyBuckets": true
}
```

Response:

```json
{
  "startUtc": "2026-06-11T05:00:00Z",
  "endUtc": "2026-06-11T06:00:00Z",
  "bucketMinutes": 60,
  "totalSamples": 2,
  "buckets": [
    {
      "bucketStartUtc": "2026-06-11T05:00:00Z",
      "bucketEndUtc": "2026-06-11T06:00:00Z",
      "sampleCount": 2,
      "averageCpuUtilizationPercent": 30,
      "averageMemoryUtilizationPercent": 60,
      "averageGpuUtilizationPercent": 40,
      "minGpuUtilizationPercent": 30,
      "maxGpuUtilizationPercent": 50,
      "averageGpuTemperatureCelsius": 63
    }
  ]
}
```

`bucketMinutes` is clamped to `1..1440`. Requests where `endUtc` is not later than `startUtc` return `400`.

### `DELETE /v1/telemetry/history/{sampleId}`

Deletes one sample and cascades child GPU rows.

Response:

```json
{
  "deleted": true,
  "deletedCount": 1
}
```

### `DELETE /v1/telemetry/history`

Deletes samples matching a `TelemetryHistorySearchFilter` request body. Use the same filter shape as `POST /v1/telemetry/history/search`.

Response:

```json
{
  "deleted": true,
  "deletedCount": 12
}
```

### `GET /openapi.json`

Returns the generated OpenAPI document for the daemon.

### `GET /openapi`

Returns Swagger UI for the generated OpenAPI document.

### `GET /dashboard`

Returns the dashboard single-page application when `Dashboard.Enabled` is `true`.

### `GET /favicon.ico`

Returns the dashboard favicon sourced from `assets/icon.ico` when the dashboard is enabled.
