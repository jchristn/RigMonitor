# Utilyze Integration Plan

## Goal

Integrate Utilyze telemetry into RigMonitor as an optional telemetry section exposed through the existing `/v1/telemetry` API, without replacing the current DCGM-backed `gpu` section.

## Progress Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked or deferred

## Scope

- [x] Add Utilyze configuration to RigMonitor telemetry settings.
- [x] Add Utilyze availability to runtime capabilities.
- [x] Add Utilyze telemetry models and provider interfaces.
- [x] Consume the Utilyze live WebSocket service as an optional sidecar.
- [x] Add `utilyze` as a selectable telemetry section.
- [x] Include `collection.utilyze` metadata.
- [x] Update API docs, README, and sample settings.
- [x] Add focused tests for parser, models, and collection status.

## Out Of Scope For First Pass

- [!] Launching or supervising the `utlz` executable from RigMonitor.
- [!] Embedding Utilyze Go/native/CUPTI code in the .NET process.
- [!] Replacing DCGM exporter telemetry.
- [!] Dashboard visualization beyond API readiness.

## Proposed API Shape

`GET /v1/telemetry?utilyze` includes:

```json
{
  "utilyzeAvailable": true,
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

## Implementation Checklist

### 1. Configuration

- [x] Add `Constants.DefaultUtilyzeLiveUrl`.
- [x] Add `TelemetrySettings.UtilyzeEnabled`.
- [x] Add `TelemetrySettings.UtilyzeLiveUrl`.
- [x] Add `TelemetrySettings.UtilyzeClientId`.
- [x] Add `TelemetrySettings.UtilyzeSampleStaleAfterMs`.
- [x] Update `rigmonitor.json`.

### 2. Core Models And Contracts

- [x] Add `UtilyzeTelemetry`.
- [x] Add `UtilyzeDeviceTelemetry`.
- [x] Add `IUtilyzeTelemetryProvider`.
- [x] Extend `TelemetrySnapshot` with `UtilyzeAvailable` and `Utilyze`.
- [x] Extend `RuntimeCapabilities` with Utilyze properties.
- [x] Extend `TelemetryCollectionMetadata` with `Utilyze`.
- [x] Extend `TelemetryRequestOptions` with `IncludeUtilyze`.

### 3. Utilyze Client

- [x] Add Utilyze WebSocket DTOs.
- [x] Add `UtilyzeTelemetryProvider`.
- [x] Connect to `Telemetry.UtilyzeLiveUrl` with `client_id`.
- [x] Cache latest `init`, `metrics`, and `ceilings` events.
- [x] Return stale/unavailable when no fresh sample exists.
- [x] Keep failures non-fatal.

### 4. Capability Detection

- [x] Probe Utilyze availability during startup.
- [x] Expose configured Utilyze URL in `/v1/capabilities`.
- [x] Log Utilyze availability at startup.

### 5. Telemetry API Integration

- [x] Add `_UtilyzeSection` state in `TelemetryService`.
- [x] Add Utilyze collector call.
- [x] Add `utilyze` to `TelemetryRequestParser`.
- [x] Update OpenAPI route description.

### 6. Documentation

- [x] Update `README.md` feature list, settings, and endpoint notes.
- [x] Update `REST_API.md` capabilities and telemetry schema.
- [x] Update `RigMonitor.postman_collection.json` if needed.

### 7. Tests

- [x] Update parser tests for `utilyze`.
- [x] Update collection status tests for `utilyze`.
- [x] Add serialization coverage for Utilyze models.
- [x] Run .NET tests.

## Validation

- [x] `dotnet test src/RigMonitor.sln`
- [x] Manual smoke test without Utilyze running: `collection.utilyze.statusCode` is `unsupported` or `unavailable`, and process starts.
- [!] Manual smoke test with Utilyze running: `/v1/telemetry?utilyze` returns latest Utilyze sample.

Validation notes:

- `dotnet test src/RigMonitor.sln` passed.
- No-Utilyze smoke test returned HTTP 200, `utilyzeAvailable=false`, `collection.utilyze.statusCode=unsupported`, and no `utilyze` payload.
- Live Utilyze smoke test is deferred until a Utilyze sidecar is running.

## Notes

- Utilyze requires NVIDIA profiling permissions on the collecting Linux host. RigMonitor should report unavailable telemetry instead of failing startup when those permissions are absent.
- Utilyze may post anonymized aggregate data to Systalyze unless `UTLZ_DISABLE_METRICS=1` is set for the Utilyze process. RigMonitor should document this but should not manage that behavior.
- The first pass should consume Utilyze as a sidecar over WebSocket because it avoids embedding native CUDA/CUPTI dependencies in RigMonitor.
