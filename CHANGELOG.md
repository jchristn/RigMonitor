# Changelog

## 0.2.0-alpha

- Added production observability for RigMonitor itself: a `RigMonitor` `Meter` and `ActivitySource` (BCL only, no exporter dependency in `RigMonitor.Core` or `RigMonitor.Telemetry`) covering the snapshot workflow and every section, the background collector (job, queued/collect/persist stages, last success), retention, every outbound integration (DCGM exporter, Ollama, vLLM, Utilyze, SQLite, subprocesses), the SQLite concurrency slot, startup, capabilities, errors by type, build info, and safe configuration gauges
- Added a Radiant 0.1.2 telemetry host in `RigMonitor.Server` that subscribes to the `RigMonitor` and `Watson` sources and exports over OTLP, an in-process Prometheus scrape endpoint (default `127.0.0.1:9464`), and optionally Loki, with .NET runtime metrics and second-based histogram buckets
- Enabled Watson's built-in HTTP telemetry and W3C trace context propagation explicitly
- Added the `Observability` settings section and `RIGMONITOR_OBSERVABILITY_*` environment variable overrides
- Forwarded application log lines to Loki with trace correlation when Loki export is enabled
- Added Prometheus, Tempo, Loki, and Grafana to `docker/compose.yaml` with healthchecks, dependency ordering, and an explicit `rigmonitor` project name; added `curl` to the runtime image for the healthcheck
- Added six Grafana dashboards provisioned into a RigMonitor folder: Overview, HTTP, Snapshots & Sections, Collector & Retention, Integrations, and Runtime
- Added an External services card to the dashboard home page linking to Grafana, Prometheus, Tempo, and Loki, translated into all supported languages
- Added `docker/update.bat` and `docker/update.sh`
- Added `TELEMETRY.md` with the full metrics and spans catalog, configuration, dashboards, and recommended alerts
- Added telemetry tests using in-memory meter and activity listeners
- Changed a timed-out (not cancelled) background collection to be logged and retried on the next tick instead of silently ending the collection loop

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
- Added settings file normalization on startup so existing `rigmonitor.json` files receive newly introduced default properties
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
- Added contextual roll-up chart hover details for CPU cores, memory usage, paired network/disk metrics, GPU ranges, and runtime queues
- Added server output cleanup scripts for local runtime settings, logs, and SQLite database assets
- Added root native run scripts that build the dashboard, build the server, prepare local runtime directories, and launch RigMonitor
- Added Docker build and compose assets
- Added Docker runtime data persistence for settings, logs, SQLite databases, and SQLite WAL/SHM sidecars
