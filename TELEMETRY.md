# RigMonitor Telemetry

RigMonitor collects host telemetry for other people. This document is about the telemetry RigMonitor emits **about itself**: the metrics, traces, and logs an on-call engineer uses in Grafana to see where a slow `/v1/telemetry` call spent its time, why the background collector stopped persisting samples, or which dependency (DCGM exporter, Ollama, vLLM, Utilyze, SQLite, a subprocess) is failing.

The design follows one split. **Code emits; the host collects.**

- `RigMonitor.Core` and `RigMonitor.Telemetry` emit through the .NET base class library only: a `Meter` and an `ActivitySource`, both named `RigMonitor`. With nothing listening, every call is a cheap no-op. These libraries take no dependency on any exporter.
- Watson 7.1 emits the HTTP layer (request metrics and one server span per request) on its own `Watson` meter and activity source.
- `RigMonitor.Server` hosts one [Radiant](https://www.nuget.org/packages/Radiant) `RadiantHost` at the composition root. It subscribes to `RigMonitor` and `Watson`, adds .NET runtime and process metrics, and exports over OTLP, an in-process Prometheus scrape endpoint, and (optionally) Loki.

## Contents

- [Source names](#source-names)
- [Enabling and configuring](#enabling-and-configuring)
- [Subscribing from another host](#subscribing-from-another-host)
- [Metrics catalog](#metrics-catalog)
- [Spans catalog](#spans-catalog)
- [Logs](#logs)
- [Context propagation](#context-propagation)
- [Observability stack and dashboards](#observability-stack-and-dashboards)
- [Recommended alerts](#recommended-alerts)
- [Cardinality, privacy, and safety](#cardinality-privacy-and-safety)
- [Testing the instrumentation](#testing-the-instrumentation)
- [Known limitations](#known-limitations)

## Source names

| Name | Kind | Emitted by | Contents |
|---|---|---|---|
| `RigMonitor` | `Meter` | `RigMonitor.Core`, `RigMonitor.Telemetry`, `RigMonitor.Server` | Application metrics (`rigmonitor.*`) |
| `RigMonitor` | `ActivitySource` | same | Application spans |
| `Watson` | `Meter` and `ActivitySource` | Watson 7.1 | HTTP server metrics and one server span per request |
| `System.Runtime` and process | `Meter` | .NET runtime, via Radiant | GC, JIT, thread pool, CPU, memory |

All RigMonitor names live in one constants class, `RigMonitor.Core.Observability.RigMonitorTelemetryNames`. Treat them as a public contract: the Grafana dashboards and alerts below depend on them. The meter and activity source version is the RigMonitor assembly version.

## Enabling and configuring

Observability export is **on by default**. Configure it in the `observability` section of `rigmonitor.json`:

```json
{
  "observability": {
    "enabled": true,
    "serviceName": "rigmonitor",
    "otlpEnabled": true,
    "otlpEndpoint": "http://127.0.0.1:4317",
    "otlpProtocol": "grpc",
    "prometheusEnabled": true,
    "prometheusHostname": "127.0.0.1",
    "prometheusPort": 9464,
    "prometheusPath": "/metrics",
    "lokiEnabled": false,
    "lokiEndpoint": "http://127.0.0.1:3100/otlp",
    "lokiTenantId": null,
    "traceSamplingRatio": 1.0,
    "metricExportIntervalMs": 15000,
    "includeRuntimeMetrics": true
  }
}
```

| Setting | Default | Range / values | Effect |
|---|---|---|---|
| `enabled` | `true` | bool | Master switch. `false` starts no exporter host; instrumentation stays a no-op. |
| `serviceName` | `rigmonitor` | non-empty | `service.name` on every signal. Use a per-rig name when several rigs share a backend. |
| `otlpEnabled` | `true` | bool | Push metrics and traces over OTLP. |
| `otlpEndpoint` | `http://127.0.0.1:4317` | absolute URI | Collector or Tempo endpoint. Invalid values fall back to the default. |
| `otlpProtocol` | `grpc` | `grpc`, `httpprotobuf` | Use port 4317 with `grpc`, 4318 with `httpprotobuf`. |
| `prometheusEnabled` | `true` | bool | Serve an in-process Prometheus scrape endpoint on its own port. |
| `prometheusHostname` | `127.0.0.1` | hostname or IP | Host the scrape listener binds to **and** the `Host` header it accepts. Wildcards (`*`, `+`, `0.0.0.0`) are not supported by the listener. In a container, use the container's DNS name (the bundled compose file uses `rigmonitor`). |
| `prometheusPort` | `9464` | 1 to 65535 | Scrape port. Must differ from `webserver.port`. |
| `prometheusPath` | `/metrics` | path | Scrape path. |
| `lokiEnabled` | `false` | bool | Export logs directly to Loki 3.x over OTLP/HTTP. Logs are exported only when this is `true`. |
| `lokiEndpoint` | `http://127.0.0.1:3100/otlp` | absolute URI | Loki OTLP base URL (`/v1/logs` is appended). |
| `lokiTenantId` | `null` | string | Sent as `X-Scope-OrgID` for multi-tenant Loki. |
| `traceSamplingRatio` | `1.0` | 0 to 1 | Parent-based sampling ratio. |
| `metricExportIntervalMs` | `15000` | 1000 to 300000 | OTLP metric push cadence. |
| `includeRuntimeMetrics` | `true` | bool | Export .NET runtime and process metrics. |

The loopback defaults are `127.0.0.1`, not `localhost`: on Windows `localhost` resolves to IPv6 `::1` first and stalls before falling back.

### Environment variable overrides

Every setting above can be overridden at startup without editing (or persisting to) `rigmonitor.json`. Unset or unparseable values are ignored.

| Variable | Overrides |
|---|---|
| `RIGMONITOR_OBSERVABILITY_ENABLED` | `enabled` (`true`/`false`/`1`/`0`) |
| `RIGMONITOR_OBSERVABILITY_SERVICE_NAME` | `serviceName` |
| `RIGMONITOR_OBSERVABILITY_OTLP_ENABLED` | `otlpEnabled` |
| `RIGMONITOR_OBSERVABILITY_OTLP_ENDPOINT` | `otlpEndpoint` |
| `RIGMONITOR_OBSERVABILITY_OTLP_PROTOCOL` | `otlpProtocol` |
| `RIGMONITOR_OBSERVABILITY_PROMETHEUS_ENABLED` | `prometheusEnabled` |
| `RIGMONITOR_OBSERVABILITY_PROMETHEUS_HOSTNAME` | `prometheusHostname` |
| `RIGMONITOR_OBSERVABILITY_PROMETHEUS_PORT` | `prometheusPort` |
| `RIGMONITOR_OBSERVABILITY_LOKI_ENABLED` | `lokiEnabled` |
| `RIGMONITOR_OBSERVABILITY_LOKI_ENDPOINT` | `lokiEndpoint` |
| `RIGMONITOR_OBSERVABILITY_LOKI_TENANT_ID` | `lokiTenantId` |
| `RIGMONITOR_OBSERVABILITY_TRACE_SAMPLING_RATIO` | `traceSamplingRatio` |

### Watson settings

`RigMonitorServer` sets Watson's `Settings.Telemetry.Enable`, `EnableMetrics`, `EnableTraces`, and `PropagateContext` to `true` explicitly. Watson's own in-process Prometheus endpoint stays **off**, because the Radiant scrape endpoint already serves Watson's series alongside RigMonitor's and the runtime's. Forwarded-header trust stays off (Watson's default).

### Failure behavior

Instrumentation is best-effort. If the exporter host cannot start (for example the scrape port is taken or the hostname cannot be bound), RigMonitor logs a warning that includes the underlying cause, such as `Observability export disabled because initialization failed: ... -> HttpListenerException: ...`, and keeps serving the API without export. A failed metric write or span export never affects a request or the collector.

## Subscribing from another host

`RigMonitor.Core` and `RigMonitor.Telemetry` can be embedded without `RigMonitor.Server`. They never reference an exporter. To collect their telemetry, subscribe to the names:

```csharp
// Radiant
RadiantSettings settings = new RadiantSettings("my-host");
settings.Sources.AddMeter("RigMonitor");
settings.Sources.AddActivitySource("RigMonitor");
using (RadiantHost host = RadiantHost.Start(settings)) { /* run */ }
```

```csharp
// OpenTelemetry SDK
Sdk.CreateMeterProviderBuilder().AddMeter("RigMonitor").AddOtlpExporter().Build();
Sdk.CreateTracerProviderBuilder().AddSource("RigMonitor").AddOtlpExporter().Build();
```

`RigMonitor.Server.Services.RigMonitorMetricCatalog.All` holds Radiant `Convention`s for every RigMonitor histogram with explicit bucket boundaries in seconds (0.5 ms to 60 s). Register it with `settings.Metrics.DefineAll(RigMonitorMetricCatalog.All)`, or apply equivalent OpenTelemetry views, so p95 queries resolve sensibly.

## Metrics catalog

Prometheus names are what the OpenTelemetry Prometheus exporter produces: dots become underscores, counters gain `_total`, and units add `_seconds`. Histograms expose `_bucket`, `_sum`, and `_count`. Every series also carries `otel_scope_name` and `otel_scope_version`.

Outcome values used across the catalog: `ok`, `error`, `timeout` (a deadline elapsed), `cancelled` (the caller or shutdown cancelled), `http_error` (non-success HTTP status), `unavailable` (dependency returned no usable data), `skipped` (concurrency slot busy), `degraded` (snapshot completed but a requested, supported section returned no data).

### Snapshot workflow (core workflow)

| Instrument | Prometheus name | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `rigmonitor.snapshot.duration` | `rigmonitor_snapshot_duration_seconds` | Histogram | s | `rigmonitor.snapshot.scope` (`full`, `partial`), `outcome` | End-to-end time to build one telemetry snapshot (API request or collector job). |
| `rigmonitor.snapshot.count` | `rigmonitor_snapshot_count_total` | Counter | {snapshot} | `rigmonitor.snapshot.scope`, `outcome` (`ok`, `degraded`, `cancelled`, `error`) | Snapshots by outcome. |
| `rigmonitor.section.duration` | `rigmonitor_section_duration_seconds` | Histogram | s | `rigmonitor.section`, `rigmonitor.section.status` | Time to collect one section. Not recorded for `disabled` or `unsupported` skips. |
| `rigmonitor.section.collections` | `rigmonitor_section_collections_total` | Counter | {collection} | `rigmonitor.section`, `rigmonitor.section.status` | Section collections by status. |
| `rigmonitor.section.last_success` | `rigmonitor_section_last_success_seconds` | Gauge | s | `rigmonitor.section` | Unix time of the last successful collection per section. |

`rigmonitor.section`: `system`, `cpu`, `memory`, `network`, `disk`, `gpu`, `ollama`, `vllm`, `utilyze`.
`rigmonitor.section.status`: `ok`, `unavailable`, `error`, `timeout`, `unsupported`, `disabled`.

### Background collector pipeline and retention

| Instrument | Prometheus name | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `rigmonitor.collector.jobs` | `rigmonitor_collector_jobs_total` | Counter | {job} | `outcome` (`ok`, `error`, `timeout`, `skipped`) | Collector jobs (collect plus persist) by outcome. |
| `rigmonitor.collector.job.duration` | `rigmonitor_collector_job_duration_seconds` | Histogram | s | `outcome` | Duration of one job. |
| `rigmonitor.collector.stage.duration` | `rigmonitor_collector_stage_duration_seconds` | Histogram | s | `rigmonitor.stage` (`queued`, `collect`, `persist`), `outcome` | Per-stage duration. `queued` is the lag between the scheduled tick and the start of work. |
| `rigmonitor.collector.stage.events` | `rigmonitor_collector_stage_events_total` | Counter | {event} | `rigmonitor.stage`, `outcome` | Per-stage events. A busy slot records `queued`/`skipped`. |
| `rigmonitor.collector.last_success` | `rigmonitor_collector_last_success_seconds` | Gauge | s | none | Unix time of the last successful job. |
| `rigmonitor.collector.in_progress` | `rigmonitor_collector_in_progress` | UpDownCounter | {job} | none | Collector slot in use (capacity 1). |
| `rigmonitor.retention.runs` | `rigmonitor_retention_runs_total` | Counter | {run} | `outcome` | Retention prune runs. |
| `rigmonitor.retention.duration` | `rigmonitor_retention_duration_seconds` | Histogram | s | `outcome` | Duration of one prune run. |
| `rigmonitor.retention.last_success` | `rigmonitor_retention_last_success_seconds` | Gauge | s | none | Unix time of the last successful prune run. |
| `rigmonitor.samples.persisted` | `rigmonitor_samples_persisted_total` | Counter | {sample} | none | Samples written to the history database. |
| `rigmonitor.samples.deleted` | `rigmonitor_samples_deleted_total` | Counter | {sample} | `rigmonitor.delete.reason` (`retention`, `api`, `api_bulk`) | Samples deleted. |

### Outbound integrations and the database slot

| Instrument | Prometheus name | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `rigmonitor.integration.requests` | `rigmonitor_integration_requests_total` | Counter | {request} | `rigmonitor.integration.service`, `rigmonitor.integration.operation`, `outcome` | Every outbound call. |
| `rigmonitor.integration.duration` | `rigmonitor_integration_duration_seconds` | Histogram | s | same | Outbound call latency. |
| `rigmonitor.db.lock.wait_duration` | `rigmonitor_db_lock_wait_duration_seconds` | Histogram | s | `outcome` | Time spent waiting for the SQLite concurrency slot. |
| `rigmonitor.db.lock.waiting` | `rigmonitor_db_lock_waiting` | UpDownCounter | {caller} | none | Callers waiting for the slot. |
| `rigmonitor.db.lock.in_use` | `rigmonitor_db_lock_in_use` | UpDownCounter | {slot} | none | Slots held. |
| `rigmonitor.db.lock.capacity` | `rigmonitor_db_lock_capacity` | Gauge | {slot} | none | Slot capacity (always 1: SQLite access is serialized). |
| `rigmonitor.utilyze.connected` | `rigmonitor_utilyze_connected` | Gauge | 1 | none | Utilyze live WebSocket connected (1) or not (0). Absent until Utilyze is first used. |
| `rigmonitor.utilyze.messages` | `rigmonitor_utilyze_messages_total` | Counter | {message} | `rigmonitor.utilyze.event` (`init`, `metrics`, `ceilings`, `unknown`) | Messages received from Utilyze. |

| `rigmonitor.integration.service` | `rigmonitor.integration.operation` values |
|---|---|
| `dcgm` | `metrics` |
| `ollama` | `probe`, `version`, `tags`, `ps` |
| `vllm` | `metrics` |
| `utilyze` | `health`, `connect` |
| `sqlite` | `initialize`, `create`, `read`, `read_detail`, `enumerate`, `search`, `rollup`, `delete`, `delete_bulk`, `prune` |
| `process` | the executable name: `top`, `sysctl`, `vm_stat` (macOS sampling) |

### Errors, lifecycle, and configuration

| Instrument | Prometheus name | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `rigmonitor.errors` | `rigmonitor_errors_total` | Counter | {error} | `rigmonitor.component`, `error.type` | Errors by component and exception type name (or a bounded outcome such as `http_error`). |
| `rigmonitor.startup.duration` | `rigmonitor_startup_duration_seconds` | Histogram | s | `outcome` | Startup initialization (capability probe, warmup, database init). |
| `rigmonitor.capability.available` | `rigmonitor_capability_available` | Gauge | 1 | `rigmonitor.capability` (`nvidia`, `ollama`, `vllm`, `utilyze`) | Capabilities detected at startup. |
| `rigmonitor.telemetry.warm` | `rigmonitor_telemetry_warm` | Gauge | 1 | none | Samplers warm (1) or warming (0). |
| `rigmonitor.build.info` | `rigmonitor_build_info` | Gauge | 1 | `rigmonitor.version`, `process.runtime.version` | Always 1. |
| `rigmonitor.config.collection_interval` | `rigmonitor_config_collection_interval_seconds` | Gauge | s | none | `persistence.collectionIntervalMs`. |
| `rigmonitor.config.retention` | `rigmonitor_config_retention_seconds` | Gauge | s | none | `persistence.retentionDays`. |
| `rigmonitor.config.request_timeout` | `rigmonitor_config_request_timeout_seconds` | Gauge | s | none | `telemetry.requestTimeoutMs`. |
| `rigmonitor.config.section_stale_after` | `rigmonitor_config_section_stale_after_seconds` | Gauge | s | none | `telemetry.sectionStaleAfterMs`. |
| `rigmonitor.config.persistence_enabled` | `rigmonitor_config_persistence_enabled` | Gauge | 1 | none | `persistence.enabled`. |

`rigmonitor.component` values: `snapshot`, `warmup`, `capabilities`, `startup`, `section.<section>`, `collector.collect`, `collector.persist`, `collector.loop`, `retention`, `retention.loop`, `utilyze.stream`, and each integration service name.

### Watson HTTP metrics (built in)

Watson emits these itself; RigMonitor does not duplicate them. The full list is in Watson's `TELEMETRY.md`. The dashboards use `http_server_request_duration_seconds` (labels `http_request_method`, `http_route`, `http_response_status_code`), `http_server_active_requests`, `watson_server_up`, `watson_server_uptime_seconds`, `watson_server_connections_active`, `watson_server_received_bytes_total`, `watson_server_sent_bytes_total`, `watson_server_exceptions_total`, `watson_server_requests_aborted_total`, and `watson_route_unmatched_total`. Watson reports route templates, for example `/v1/telemetry/` and `/v1/telemetry/history/{sampleId}/`.

### Runtime metrics

With `includeRuntimeMetrics`, Radiant adds the .NET runtime meter (`dotnet_gc_collections_total`, `dotnet_gc_pause_time_seconds_total`, `dotnet_gc_last_collection_heap_size_bytes`, `dotnet_process_cpu_time_seconds_total`, `dotnet_process_memory_working_set_bytes`, `dotnet_thread_pool_thread_count_total`, `dotnet_thread_pool_queue_length_total`, `dotnet_exceptions_total`, `dotnet_monitor_lock_contentions_total`, and more) plus `process_*` metrics.

## Spans catalog

| Span | Kind | Parent | Key attributes | Status |
|---|---|---|---|---|
| `{METHOD} {route}` (Watson) | Server | inbound `traceparent`, else root | `http.request.method`, `http.route`, `http.response.status_code`, `url.scheme`, client address | Error on 5xx or unhandled exception |
| `telemetry.snapshot` | Internal | Watson server span, or `stage:collect` | `rigmonitor.snapshot.scope`, `outcome` | Ok; Error with exception on failure; unset on cancellation |
| `stage:<section>` (`stage:cpu`, `stage:gpu`, ...) | Internal | `telemetry.snapshot` | `rigmonitor.section`, `rigmonitor.section.status`, `error.type` | Ok; Error for `error`, `timeout`, `unavailable` |
| `collector.job` | Internal | **root** (one trace per job) | `rigmonitor.collector.trigger` (`startup`, `timer`), `rigmonitor.persistence.hostname`, `outcome`, `rigmonitor.failed_stage` | Ok or Error |
| `stage:queued` | Internal | `collector.job` | none (start time backdated to the scheduled tick) | Ok |
| `stage:collect` | Internal | `collector.job` | none; contains `telemetry.snapshot` | Ok or Error |
| `stage:persist` | Internal | `collector.job` | `rigmonitor.sample.id`; contains `sqlite create` | Ok or Error |
| `retention.run` | Internal | **root** | `rigmonitor.retention.days`, `rigmonitor.retention.deleted` | Ok or Error |
| `rigmonitor.startup` | Internal | root | `rigmonitor.version` | Ok or Error |
| `capabilities.probe` | Internal | `rigmonitor.startup` | `rigmonitor.capability.<name>` booleans | Ok or Error |
| `telemetry.warmup` | Internal | `rigmonitor.startup` | `rigmonitor.warmup.delay_ms` | Ok or Error |
| `<service> <operation>` (`dcgm metrics`, `ollama tags`, `vllm metrics`, `utilyze health`, `utilyze connect`, `sqlite rollup`, ...) | Client | caller | `rigmonitor.integration.service`, `rigmonitor.integration.operation`, `outcome`, `server.address`, `server.port`, `url.scheme`, `http.response.status_code`, `db.system.name`, `db.operation.name` | Ok; Error with exception or `error.type` |
| `process <executable>` | Internal | caller | `process.executable.name`, `process.exit.code` | Error on non-zero exit |
| `utilyze connection` | Client | **root**, with a span link to the request that started the reader | `server.address`, `server.port`, `rigmonitor.utilyze.messages` | Error when the connection drops or fails |

A history API request, for example `POST /v1/telemetry/history/rollups`, produces `POST /v1/telemetry/history/rollups/` (Watson) with a `sqlite rollup` client span underneath, so slow roll-ups resolve to database time versus serialization.

Span attributes never include URLs with query strings, credentials, request or response bodies, or telemetry payloads. Only `server.address`, `server.port`, and `url.scheme` of a configured endpoint are recorded.

## Logs

RigMonitor's file, console, and syslog logging is unchanged. When `lokiEnabled` is `true`, every log line is also written through Radiant's OTLP log pipeline to Loki with `service_name="rigmonitor"`. Lines written inside a span carry `trace_id` and `span_id`, so a Loki line links to its Tempo trace (the provisioned Loki datasource adds a TraceID link). Logs are the third signal here because RigMonitor runs background work (the collector and retention loops).

Query examples:

```logql
{service_name="rigmonitor"} |= "Failed to persist"
{service_name="rigmonitor", detected_level="warn"}
```

## Context propagation

- **Inbound**: Watson adopts a W3C `traceparent` header (`PropagateContext = true`), so a caller's trace continues into RigMonitor.
- **In process**: application spans start under `Activity.Current`, so they nest under Watson's server span automatically.
- **Outbound HTTP**: every client span is `Activity.Current` while `HttpClient` sends, and .NET's HTTP handler injects `traceparent`, so the DCGM exporter, Ollama, vLLM, and Utilyze see the RigMonitor trace.
- **Background hand-offs**: the collector and retention loops clear the ambient context when they start, so each job is its own trace instead of hanging off the startup span. The Utilyze reader, which outlives the request that starts it, runs as its own root span with a **span link** back to the triggering request.

## Observability stack and dashboards

`docker/compose.yaml` brings up RigMonitor with the full stack under the Compose project name `rigmonitor`:

```bash
cd docker
docker compose up -d --build
```

| Service | Image | Host port | Notes |
|---|---|---|---|
| RigMonitor | built from `docker/Dockerfile` | 9990 | Scrape endpoint `rigmonitor:9464` is internal only. |
| Prometheus | `prom/prometheus:v3.5.4` | 9090 | Scrapes `rigmonitor:9464/metrics` using the classic text format. |
| Tempo | `grafana/tempo:2.6.1` | 3200, 4317, 4318 | Receives OTLP traces. |
| Loki | `grafana/loki:3.3.0` | 3100 | Receives OTLP logs. |
| Grafana | `grafana/grafana-oss:13.0.2` | 3000 | `admin` / `admin` locally; override with `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD`. |

Startup order uses healthchecks: Tempo and Loki come up healthy before RigMonitor, RigMonitor before Prometheus, and Prometheus, Tempo, and Loki before Grafana. Configuration lives in `docker/prometheus.yaml`, `docker/tempo.yaml`, `docker/loki.yaml`, and `docker/grafana/provisioning/`. Datasource UIDs are fixed: `prometheus`, `tempo`, `loki`.

Dashboards are provisioned from `assets/grafana/` into the **RigMonitor** folder, one per domain:

| Dashboard | UID | Answers |
|---|---|---|
| RigMonitor / Overview | `rigmonitor-overview` | Is it up, warm, and persisting? Request rate, 5xx ratio, snapshot p95, degraded snapshots, errors by component, detected capabilities. Start here. |
| RigMonitor / HTTP | `rigmonitor-http` | Which route is slow or failing? Rate by route and status class, p50/p95/p99, p95 by route, 5xx by route, connections, throughput. |
| RigMonitor / Snapshots & Sections | `rigmonitor-collection` | Which telemetry section dominates latency or fails? Snapshot outcomes, p95 per section, failures by status, seconds since last success. |
| RigMonitor / Collector & Retention | `rigmonitor-pipeline` | Is the background pipeline healthy? Jobs by outcome, per-stage p95 (queued/collect/persist), slot usage, samples persisted/deleted, retention, SQLite slot waits, correlated logs. |
| RigMonitor / Integrations | `rigmonitor-integrations` | Is a dependency the cause? Calls and error ratio by service, p95 by service and operation, errors by type, Utilyze stream state. |
| RigMonitor / Runtime | `rigmonitor-runtime` | Is the process itself unhealthy? CPU, memory, GC, thread pool, exceptions, lock contention. |

The product dashboard's home page carries an **External services** card linking to Grafana, Prometheus, Tempo, and Loki on the browser's current host with the default credentials.

## Recommended alerts

```yaml
groups:
  - name: rigmonitor
    rules:
      - alert: RigMonitorDown
        expr: up{job="rigmonitor"} == 0 or max(watson_server_up) == 0
        for: 2m
      - alert: RigMonitorCollectorStalled
        # No sample persisted for three collection intervals.
        expr: time() - max(rigmonitor_collector_last_success_seconds) > 3 * max(rigmonitor_config_collection_interval_seconds)
          and max(rigmonitor_config_persistence_enabled) == 1
        for: 5m
      - alert: RigMonitorCollectorFailing
        expr: sum(rate(rigmonitor_collector_jobs_total{outcome="error"}[15m])) > 0
        for: 15m
      - alert: RigMonitorRetentionStalled
        expr: time() - max(rigmonitor_retention_last_success_seconds) > 6 * 3600
        for: 30m
      - alert: RigMonitorSectionStale
        # A section that used to succeed has not succeeded within twice the stale window.
        expr: time() - rigmonitor_section_last_success_seconds > 2 * scalar(max(rigmonitor_config_section_stale_after_seconds))
        for: 10m
        labels:
          severity: warning
      - alert: RigMonitorHigh5xxRatio
        expr: sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
          / sum(rate(http_server_request_duration_seconds_count[5m])) > 0.05
        for: 10m
      - alert: RigMonitorSlowSnapshots
        expr: histogram_quantile(0.95, sum by (le) (rate(rigmonitor_snapshot_duration_seconds_bucket[10m]))) > 5
        for: 15m
        labels:
          severity: warning
      - alert: RigMonitorIntegrationErrors
        expr: sum by (rigmonitor_integration_service) (rate(rigmonitor_integration_requests_total{outcome!="ok"}[10m]))
          / sum by (rigmonitor_integration_service) (rate(rigmonitor_integration_requests_total[10m])) > 0.5
        for: 15m
        labels:
          severity: warning
      - alert: RigMonitorDatabaseContention
        expr: histogram_quantile(0.95, sum by (le) (rate(rigmonitor_db_lock_wait_duration_seconds_bucket[10m]))) > 1
        for: 10m
        labels:
          severity: warning
```

## Cardinality, privacy, and safety

- Every metric label has a small, fixed value set listed above. Sample ids, hostnames, URLs, model names, GPU ids, and query filters go on spans (or nowhere), never on metrics. `error.type` is an exception type name.
- No secrets, credentials, request bodies, or telemetry payloads are recorded on any signal. Endpoint URLs contribute only host, port, and scheme.
- Quantiles are derived in Grafana from histogram buckets; nothing is precomputed in process.
- The scrape endpoint, Prometheus, Tempo, and Loki have no authentication. Keep them on an internal network. Change Grafana's admin password for any non-local deployment.

## Testing the instrumentation

`src/Test.Shared/TelemetryCapture.cs` attaches a BCL `MeterListener` and `ActivityListener` to the `RigMonitor` and `Watson` sources. The `Observability*Tests` classes in `src/Test.Xunit` use it to verify, without a collector:

- the snapshot root span, one `stage:<section>` child per section, per-section metrics, the degraded outcome, and timeout, unavailable, unsupported, error, and cancellation paths;
- client spans, integration counters, http_error and connection failures, and `traceparent` propagation for DCGM, Ollama, vLLM, Utilyze, and subprocesses;
- SQLite client spans, sample counters, slot metrics, and database failures;
- the collector job root with queued/collect/persist stages, collect and persist failures, retention runs, and last-success gauges;
- settings defaults, environment overrides, the Radiant host's scrape endpoint and histogram buckets, best-effort startup failure, and the log sink;
- an end-to-end request through the real server where Watson adopts an inbound `traceparent` and `telemetry.snapshot` nests under Watson's server span;
- the no-listener path, which must never throw.

## Known limitations

- The Prometheus scrape listener cannot bind a wildcard host. Bind it to a concrete hostname that scrapers use in the `Host` header (the compose file uses the container name), or rely on OTLP.
- Prometheus 3 must scrape with `scrape_protocols: ['PrometheusText0.0.4']` (already set in `docker/prometheus.yaml`); with OpenMetrics negotiation it rejects the exporter's unit metadata for `watson.server.received.bytes`.
- When Loki export is enabled, log records are also pushed to the OTLP endpoint (Radiant uses one OTLP target for all signals). Tempo ignores them.
- The `utilyze connection` span lasts as long as the WebSocket connection, which can be hours.
- Docker healthchecks call `/livez` every 5 seconds, which produces `GET /livez` traces. Lower `traceSamplingRatio` or filter them in Tempo if they are noisy.
