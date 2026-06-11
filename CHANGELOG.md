# Changelog

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
- Added server output cleanup scripts for local runtime settings, logs, and SQLite database assets
- Added root native run scripts that build the dashboard, build the server, prepare local runtime directories, and launch RigMonitor
- Added Docker build and compose assets
- Added Docker runtime data persistence for settings, logs, SQLite databases, and SQLite WAL/SHM sidecars
