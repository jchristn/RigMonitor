# Changelog

## 1.0.0

- Initial RigMonitor implementation
- Added Watson 7 telemetry API host
- Added same-port React dashboard
- Added DCGM-based NVIDIA telemetry support
- Added Ollama detection and telemetry
- Added optional vLLM Prometheus telemetry support and dashboard card
- Added optional Utilyze sidecar telemetry support for GPU SOL, bandwidth, and attainable ceiling metrics
- Added dashboard Utilyze badges and telemetry card
- Added configurable telemetry persistence using SQLite with provider-neutral database interfaces
- Added PrettyId K-sortable telemetry sample IDs with prefixed IDs capped at 32 characters
- Added persisted hostname support and default `localhost` fallback for telemetry history tables
- Added background collection cadence and retention pruning settings with 30-day default retention
- Added telemetry history enumerate, search, detail, roll-up, status, and delete APIs
- Added dashboard analytics page for filtering, charting, paging, and inspecting historical telemetry samples
- Added Docker build and compose assets
