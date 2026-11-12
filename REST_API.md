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
  "hideUnsupportedGpuSections": false,
  "nvidiaAvailable": false,
  "amdAvailable": false,
  "appleGpuAvailable": true,
  "qualcommAvailable": false,
  "gpuAvailable": true,
  "appleFeatures": ["ioKit", "metal", "processInfo", "ioReport", "hidSensors"],
  "gpuProviders": [
    { "vendor": "nvidia", "available": false, "endpoint": "http://localhost:9400/metrics", "detail": "DCGM exporter is unreachable or does not emit DCGM_FI_DEV metrics.", "features": [] },
    { "vendor": "amd", "available": false, "detail": "AMD telemetry is supported on Linux and Windows.", "features": [] },
    { "vendor": "apple", "available": true, "source": "appleNative", "endpoint": "ioKit", "detail": "Apple M5 Max", "features": ["ioKit", "metal", "processInfo", "ioReport", "hidSensors"] },
    { "vendor": "qualcomm", "available": false, "detail": "Qualcomm telemetry is supported on Windows and Linux.", "features": [] }
  ],
  "ollamaAvailable": true,
  "vllmEnabled": false,
  "vllmAvailable": false,
  "utilyzeEnabled": false,
  "utilyzeAvailable": false,
  "dcgmExporterUrl": "http://localhost:9400/metrics",
  "amdMetricsExporterUrl": "http://localhost:5000/metrics",
  "ollamaBaseUrl": "http://localhost:11434",
  "vllmMetricsUrl": "http://localhost:8000/metrics",
  "utilyzeLiveUrl": "ws://127.0.0.1:8079/live"
}
```

GPU capability fields:

- `nvidiaAvailable`, `amdAvailable`, `appleGpuAvailable`, `qualcommAvailable`: whether each vendor was detected at startup.
- `gpuAvailable`: whether any vendor was detected.
- `amdSource`, `qualcommSource`: the resolved source (for example `amdSmiLibrary`, `amdSysfs`, `amdMetricsExporter`, `windowsGraphicsKernel`, `drmMsm`, `kgsl`); omitted when the vendor is unavailable.
- `appleFeatures`: native macOS features that loaded (`ioKit`, `metal`, `processInfo`, `ioReport`, `hidSensors`).
- `gpuProviders`: the probe result for every vendor, including the reason a vendor is unavailable.
- `hideUnsupportedGpuSections`: the dashboard default from `Dashboard.HideUnsupportedGpuSections`.

### `GET /v1/telemetry`

Returns a host telemetry snapshot. By default all supported sections are included.

Top-level fields:

- `collectedUtc`
- `hostPlatform`
- `nvidiaAvailable`
- `amdAvailable`
- `appleGpuAvailable`
- `qualcommAvailable`
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
- `nvidia`
- `amd`
- `apple`
- `qualcomm`
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
  "vllmAvailable": false,
  "utilyzeAvailable": false,
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
- `collection.nvidia`
- `collection.amd`
- `collection.apple`
- `collection.qualcomm`
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

`collection.gpu` is derived from the vendor sections rather than collected on its own:

- `supported` is `true` when any vendor is supported.
- `statusCode` is `ok` when at least one requested, supported vendor returned data. Otherwise it is the most severe vendor status, in the order `error` > `stale` > `unavailable` > `unsupported`.
- `lastAttemptUtc`, `lastSuccessUtc`, and `lastDurationMs` are the latest (or longest) values across vendors; vendors are collected in parallel.
- `message` appends the vendors that are not `ok` (for example `"... (amd: error)"`), and `lastError` joins vendor errors.

#### Selective telemetry query parameters

Recognized selector keys:

- `system`
- `cpu`
- `memory`
- `network`
- `disk`
- `gpu`
- `nvidia`
- `amd`
- `apple`
- `qualcomm`
- `ollama`
- `vllm`
- `utilyze`

Rules:

- If no recognized selector keys are present, all sections are included by default.
- Presence of a key means `true`.
- A key set to `=false` means `false`.
- Once one or more recognized selector keys are present, unspecified recognized sections are treated as `false`.
- `gpu` collects every vendor internally but emits only the aggregate. `nvidia`, `amd`, `apple`, and `qualcomm` emit their vendor sections. A vendor that is collected only to build the aggregate reports `disabled` in its own collection entry.

| Request | Emitted GPU sections |
|---------|----------------------|
| no selectors | `gpu`, `nvidia`, `amd`, `apple`, `qualcomm` |
| `?gpu` | `gpu` |
| `?amd` | `amd` |
| `?gpu&apple` | `gpu`, `apple` |

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

- `nvidia`, `amd`, `apple`, and `qualcomm` are present only when that vendor is supported, requested, and a current sample succeeds.
- `gpu` is present when it is requested and at least one vendor returned a device.
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

#### GPU sections

Every GPU device, in every section, shares a common shape:

- `ordinal`: zero-based position across all vendors on the host
- `deviceIndex`: index reported by the vendor source
- `vendor`: `nvidia`, `amd`, `apple`, `qualcomm`, or `intel`
- `source`: `dcgmExporter`, `amdSmiLibrary`, `amdSysfs`, `amdMetricsExporter`, `appleNative`, `windowsGraphicsKernel`, `drmMsm`, or `kgsl`
- `uuid`, `busId`, `model`, `driverVersion`
- `unifiedMemory`: whether the device uses memory shared with the host
- `metrics`:
  - `gpuUtilizationPercent`
  - `memoryUsedMegabytes`, `memoryFreeMegabytes`, `memoryTotalMegabytes`, `memoryUtilizationPercent`
  - `memorySource` (`framebuffer`, `vram`, `vramPlusGtt`, `unifiedSystemMemory`, `sharedSystemMemory`, `dedicatedVideoMemory`) and `memoryShared`
  - `temperatureCelsius`, `powerUsageWatts`, `smClockMHz` (the primary compute clock for every vendor), `memoryClockMHz`

Optional metrics are omitted when the source cannot provide them. They are never reported as `0` in place of "unknown".

| Metric | NVIDIA | AMD | Apple | Qualcomm |
|--------|--------|-----|-------|----------|
| `gpuUtilizationPercent` | yes | yes | yes (device utilization) | yes (busiest 3D/compute engine on Windows; fdinfo or kgsl on Linux) |
| memory | framebuffer | VRAM (VRAM + GTT on APUs) | unified memory in use / host memory | shared + dedicated usage / budget |
| `temperatureCelsius` | yes | edge, else hotspot | GPU sensors, else SoC die sensors | driver perf data or thermal zones |
| `powerUsageWatts` | yes | yes | yes (IOReport) | no |
| `smClockMHz` | SM clock | graphics clock | weighted active frequency | current frequency |
| `memoryClockMHz` | yes | yes | no | Windows perf data only |

`gpu` (the aggregate) contains `vendor` (`nvidia`, `amd`, `apple`, `qualcomm`, or `mixed`), `vendors` (vendors present), and `devices` in the common shape only, ordered by vendor then index. The device objects are the same ones reported in the vendor sections.

`nvidia` contains `source` (`dcgmExporter`), `endpoint`, and `devices`. NVIDIA devices add `migProfile`, `xidErrors`, `memoryTemperatureCelsius`, `memoryCopyUtilizationPercent`, and `uncorrectableEccErrors`. `memoryTotalMegabytes` is read from `DCGM_FI_DEV_FB_TOTAL` when present, otherwise derived from `DCGM_FI_DEV_FB_USED + DCGM_FI_DEV_FB_FREE`. MIG instances are reported as separate devices.

`amd` contains `source`, `gapFillSource` (`amdSysfs` when sysfs filled fields the AMD SMI library could not provide), `libraryVersion`, `endpoint`, and `devices`. AMD devices add `hotspotTemperatureCelsius`, `memoryTemperatureCelsius`, `memoryActivityPercent`, `multimediaActivityPercent`, `gttUsedMegabytes`, `gttTotalMegabytes`, `correctableEccErrors`, `uncorrectableEccErrors`, `partitionId`, `computePartition`, `memoryPartition`, and `fieldSources` (field name to the source that supplied it).

`apple` contains `source` (`appleNative`), `chip`, `gpuCoreCount`, `metalFamily` (for example `apple10`), `thermalState` (`nominal`, `fair`, `serious`, `critical`), `temperatureSource` (`gpuSensors` or `socDie`), `hostMemoryTotalMegabytes`, `recommendedMaxWorkingSetMegabytes` (Metal's recommended GPU working set), `wiredLimitMegabytes` (`iogpu.wired_limit_mb`, omitted when the OS default applies), `features`, and `devices`. Apple devices add `gpuCoreCount`, `rendererUtilizationPercent`, `tilerUtilizationPercent`, `allocatedSystemMemoryMegabytes`, `activeResidencyPercent`, `maxTemperatureCelsius`, `recommendedMaxWorkingSetMegabytes`, and `thermalState`.

`qualcomm` contains `source`, `endpoint`, and `devices`. Qualcomm devices add `adapterLuid`, `dxgiDeviceId` (Windows), `compatible` (Linux device tree), `utilizationScope` (`device`, `allProcesses`, or `ownProcesses`; on Linux, fdinfo only covers processes RigMonitor may read), `engineUtilization` (`3d`, `compute`, `copy`, `videoDecode`, `videoEncode`, ...), `sharedMemoryUsedMegabytes`, `sharedMemoryBudgetMegabytes`, `dedicatedMemoryUsedMegabytes`, `dedicatedMemoryBudgetMegabytes`, `currentFrequencyMHz`, `maxFrequencyMHz`, `maxTemperatureCelsius`, and `fieldSources`.

Unified memory: when a device flagged `unifiedMemory` does not report a capacity, RigMonitor fills `memoryTotalMegabytes` from host physical memory (and `memoryUsedMegabytes` too when the device reports no usage), sets `memorySource` to `unifiedSystemMemory`, and sets `memoryShared` to `true`. This covers NVIDIA GB10 / DGX Spark.

Example (Apple Silicon):

```json
{
  "gpu": {
    "vendor": "apple",
    "vendors": ["apple"],
    "devices": [
      {
        "ordinal": 0,
        "deviceIndex": 0,
        "vendor": "apple",
        "source": "appleNative",
        "uuid": "AGXAcceleratorG17X/0",
        "busId": "AGXAcceleratorG17X",
        "model": "Apple M5 Max",
        "driverVersion": "Unix 26.6.0",
        "unifiedMemory": true,
        "metrics": {
          "gpuUtilizationPercent": 14,
          "memoryUsedMegabytes": 1684.03,
          "memoryFreeMegabytes": 129387.97,
          "memoryTotalMegabytes": 131072,
          "memoryUtilizationPercent": 1.28,
          "memorySource": "unifiedSystemMemory",
          "memoryShared": true,
          "temperatureCelsius": 63.98,
          "powerUsageWatts": 0.29,
          "smClockMHz": 338
        }
      }
    ]
  },
  "apple": {
    "source": "appleNative",
    "chip": "Apple M5 Max",
    "gpuCoreCount": 40,
    "metalFamily": "apple10",
    "thermalState": "fair",
    "temperatureSource": "socDie",
    "hostMemoryTotalMegabytes": 131072,
    "recommendedMaxWorkingSetMegabytes": 110100.48,
    "features": ["ioKit", "metal", "processInfo", "ioReport", "hidSensors"],
    "devices": [
      {
        "ordinal": 0,
        "deviceIndex": 0,
        "vendor": "apple",
        "source": "appleNative",
        "model": "Apple M5 Max",
        "unifiedMemory": true,
        "gpuCoreCount": 40,
        "rendererUtilizationPercent": 14,
        "tilerUtilizationPercent": 11,
        "allocatedSystemMemoryMegabytes": 6356.83,
        "activeResidencyPercent": 31.86,
        "maxTemperatureCelsius": 66.51,
        "recommendedMaxWorkingSetMegabytes": 110100.48,
        "thermalState": "fair",
        "metrics": { "gpuUtilizationPercent": 14, "memoryUsedMegabytes": 1684.03, "memoryTotalMegabytes": 131072, "powerUsageWatts": 0.29, "smClockMHz": 338, "temperatureCelsius": 63.98 }
      }
    ]
  }
}
```

Example (AMD with the native library and sysfs gap-fill):

```json
{
  "amd": {
    "source": "amdSmiLibrary",
    "gapFillSource": "amdSysfs",
    "libraryVersion": "6.4.0",
    "endpoint": "/opt/rocm/lib/libamd_smi.so",
    "devices": [
      {
        "ordinal": 0,
        "deviceIndex": 0,
        "vendor": "amd",
        "source": "amdSmiLibrary",
        "uuid": "uuid-0",
        "busId": "0000:0c:00.0",
        "model": "AMD Instinct MI300X",
        "unifiedMemory": false,
        "computePartition": "SPX",
        "memoryPartition": "NPS1",
        "hotspotTemperatureCelsius": 75,
        "memoryTemperatureCelsius": 60,
        "memoryActivityPercent": 40,
        "correctableEccErrors": 4,
        "uncorrectableEccErrors": 1,
        "fieldSources": { "utilization": "amdSmiLibrary", "memory": "amdSmiLibrary", "power": "amdSysfs" },
        "metrics": {
          "gpuUtilizationPercent": 91,
          "memoryUsedMegabytes": 65536,
          "memoryTotalMegabytes": 196608,
          "memorySource": "vram",
          "memoryShared": false,
          "temperatureCelsius": 75,
          "powerUsageWatts": 650,
          "smClockMHz": 2100,
          "memoryClockMHz": 1300
        }
      }
    ]
  }
}
```

Example (Qualcomm on Windows on Snapdragon):

```json
{
  "qualcomm": {
    "source": "windowsGraphicsKernel",
    "endpoint": "dxgi",
    "devices": [
      {
        "ordinal": 0,
        "deviceIndex": 0,
        "vendor": "qualcomm",
        "source": "windowsGraphicsKernel",
        "model": "Snapdragon(R) X Elite - X1E78100 - Qualcomm(R) Adreno(TM) GPU",
        "unifiedMemory": true,
        "adapterLuid": "0x00000000_0x0000D1B6",
        "dxgiDeviceId": "0x0c36",
        "utilizationScope": "device",
        "engineUtilization": { "3d": 41, "compute": 12 },
        "sharedMemoryUsedMegabytes": 2048,
        "sharedMemoryBudgetMegabytes": 16384,
        "currentFrequencyMHz": 1250,
        "maxFrequencyMHz": 1500,
        "metrics": { "gpuUtilizationPercent": 41, "memoryUsedMegabytes": 2176, "memoryTotalMegabytes": 16896, "memorySource": "sharedSystemMemory", "memoryShared": true, "smClockMHz": 1250, "temperatureCelsius": 52.5 }
      }
    ]
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

Persisted sample identifiers are PrettyId K-sortable IDs with the `tel_` prefix and a maximum length of 32 characters.

### `GET /v1/telemetry/history/status`

Returns persistence worker and database status.

Example:

```json
{
  "enabled": true,
  "hostname": "localhost",
  "collectionIntervalMs": 15000,
  "retentionDays": 30,
  "databaseType": "Sqlite",
  "databaseFilename": "data/rigmonitor.telemetry.db",
  "lastAttemptUtc": "2026-06-11T12:00:00Z",
  "lastSuccessUtc": "2026-06-11T12:00:00Z",
  "nextCollectionUtc": "2026-06-11T12:00:15Z"
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
  "gpuVendor": "nvidia",
  "nvidiaAvailable": true,
  "amdAvailable": null,
  "appleGpuAvailable": null,
  "qualcommAvailable": null,
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
      "nvidiaAvailable": true,
      "amdAvailable": false,
      "appleGpuAvailable": false,
      "qualcommAvailable": false,
      "gpuDeviceCount": 1,
      "gpuVendor": "nvidia"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1,
  "totalPages": 1
}
```

GPU search fields:

- `gpuVendor`: `nvidia`, `amd`, `apple`, `qualcomm`, or `intel`; matches samples with at least one device from that vendor.
- `nvidiaAvailable`, `amdAvailable`, `appleGpuAvailable`, `qualcommAvailable`: match the vendor availability recorded with each sample.
- Records include `gpuVendor` (`mixed` when more than one vendor reported devices). GPU power and temperature summaries are `null` when no device reported them.

### `GET /v1/telemetry/history/{sampleId}`

Returns one persisted sample with all scalar fields and the original `snapshot` payload.

Responses:

- `200 OK` with `TelemetrySampleDetail`
- `404 Not Found` when the sample does not exist

### `POST /v1/telemetry/history/rollups`

Returns bucketized aggregate telemetry over a time range. `startUtc`, `endUtc`, and `bucketMinutes` are authoritative; `includeEmptyBuckets` should be `true` for charting. The range start is inclusive and the range end is exclusive. Fractional-second request timestamps are supported; returned bucket boundaries are aligned to the bucket interval using the request start time rounded to the same whole-second precision used by SQLite grouping.

Request:

```json
{
  "hostname": "localhost",
  "startUtc": "2026-06-11T05:00:00Z",
  "endUtc": "2026-06-11T06:00:00Z",
  "bucketMinutes": 60,
  "gpuUuid": null,
  "gpuVendor": null,
  "includeGpuVendorBreakdown": true,
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
      "averageLogicalCoreCount": 16,
      "averageMemoryUtilizationPercent": 60,
      "averageMemoryTotalBytes": 1073741824,
      "averageMemoryUsedBytes": 644245094,
      "averageMemoryAvailableBytes": 429496729.5,
      "averageNetworkReceiveBytesPerSecond": 1000,
      "averageNetworkTransmitBytesPerSecond": 500,
      "averageDiskReadOperationsPerSecond": 10,
      "averageDiskWriteOperationsPerSecond": 20,
      "averageDiskReadQueueDepth": 1,
      "averageDiskWriteQueueDepth": 2,
      "averageGpuDeviceCount": 1,
      "averageGpuUtilizationPercent": 40,
      "minGpuUtilizationPercent": 30,
      "maxGpuUtilizationPercent": 50,
      "averageGpuMemoryUtilizationPercent": 50,
      "averageGpuMemoryUsedMegabytes": 12288,
      "averageGpuMemoryTotalMegabytes": 24576,
      "averageGpuTemperatureCelsius": 63,
      "minGpuTemperatureCelsius": 60,
      "maxGpuTemperatureCelsius": 66,
      "averageGpuPowerUsageWatts": 250,
      "averageOllamaAvailableModelCount": 4,
      "averageOllamaLoadedModelCount": 2,
      "averageVllmRunningRequests": 3,
      "averageVllmWaitingRequests": 1,
      "averageVllmGpuCacheUsagePercent": 44,
      "averageUtilyzeDeviceCount": 1,
      "gpuVendors": {
        "nvidia": {
          "sampleCount": 2,
          "averageDeviceCount": 1,
          "averageUtilizationPercent": 40,
          "minUtilizationPercent": 30,
          "maxUtilizationPercent": 50,
          "averageMemoryUtilizationPercent": 50,
          "averageMemoryUsedMegabytes": 12288,
          "averageMemoryTotalMegabytes": 24576,
          "averageTemperatureCelsius": 63,
          "minTemperatureCelsius": 60,
          "maxTemperatureCelsius": 66,
          "averagePowerUsageWatts": 250
        }
      }
    }
  ]
}
```

GPU roll-up fields:

- `gpuVendor` filters to samples with that vendor and recomputes the bucket's `averageGpu*` fields from that vendor's devices only.
- `includeGpuVendorBreakdown` (default `true`) adds `gpuVendors` to each bucket, keyed by vendor. Vendors with no data in a bucket are omitted.
- Each vendor entry aggregates per sample first (utilization and temperature averaged across devices; memory and power summed across devices), then averages those per-sample values across the bucket. Vendor-specific entries include `averageHotspotTemperatureCelsius` and `averageGttUsedMegabytes` (AMD), `averageWorkingSetMegabytes` and `maxThermalState` (Apple, 0 = nominal to 3 = critical), and `averageMemoryBudgetMegabytes` (Qualcomm).
- `averageGpuPowerUsageWatts` and GPU temperature fields are `null` when no device in the bucket reported them.

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
