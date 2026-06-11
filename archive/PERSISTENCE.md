# Telemetry Persistence Plan

RigMonitor currently answers live telemetry requests through `/v1/telemetry` and renders the current snapshot in the dashboard. Persistence should add a second path through the product: collect snapshots on a configured cadence, keep them in a provider-neutral database layer, expose history APIs for operators and automation, and add a dashboard analytics view that makes historical telemetry explorable without changing the live endpoint.

The implementation should follow the database, enumeration, and request-history patterns used in `C:\Code\AssistantHub`, `C:\Code\CommittedCoaches\Chronos`, `C:\Code\Verbex`, and `C:\Code\Lattice`, while staying aligned with the stricter requirements in `C:\Code\Agents`. The main local references are Lattice's `RequestHistoryService`, `IRequestHistoryMethods`, SQLite request-history implementation, and request-history dashboard, plus AssistantHub's `EnumerationQuery` / `EnumerationResult` envelope.

## Progress Tracking

Use this document as the implementation tracker. Mark each checkbox as work lands, and add short notes inline when a task is intentionally deferred, split, or completed by a different file shape than the one listed here.

Status convention:

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress or partially complete
- `[!]` Blocked, with a note explaining the blocker

Completion notes should name the main PR, commit, or files changed when useful. Example: `- [x] Add PersistenceSettings. Done in src/RigMonitor.Core/Settings/PersistenceSettings.cs.`

## Scope

Ship this as a whole-product feature, not a storage-only backend patch. The work includes Core models and settings, a database driver abstraction, a SQLite implementation, background collection and retention tasks, REST APIs, OpenAPI metadata, dashboard analytics, documentation, Postman, tests, deployment notes, and changelog updates.

RigMonitor does not currently have authentication or tenancy. Do not invent a partial auth layer inside this feature. The history routes should have the same exposure model as the existing telemetry routes, and the design should leave room for tenant/user fields later if RigMonitor adopts the `C:\Code\Agents\requirements\AUTHENTICATION.md` model.

## Configuration

Add a root `Persistence` settings section to `rigmonitor.json`. Keeping this separate from `Telemetry` avoids mixing provider endpoints with local storage behavior.

```json
{
  "Persistence": {
    "Enabled": true,
    "CollectionIntervalMs": 60000,
    "RetentionDays": 30,
    "PruneIntervalMinutes": 60,
    "Hostname": "localhost",
    "Database": {
      "Type": "Sqlite",
      "Filename": "data/rigmonitor.telemetry.db",
      "LogQueries": false
    }
  }
}
```

Required settings work:

- [x] Add `PersistenceSettings`, `DatabaseSettings`, and `DatabaseTypeEnum` under `src/RigMonitor.Core/Settings` or `src/RigMonitor.Core/Database`.
- [x] Add `RigMonitorSettings.Persistence`.
- [x] Clamp `CollectionIntervalMs` to a practical range such as `1000..86400000`.
- [x] Clamp `RetentionDays` to `1..3650`, with the required default of `30`.
- [x] Clamp `PruneIntervalMinutes` to `1..1440`.
- [x] Create the database directory automatically on startup when SQLite is selected. Done in `SqliteDatabaseDriver` and wired through server startup.
- [x] Update settings creation tests so a missing settings file writes the new defaults.

## Database Architecture

Follow the provider-neutral method-interface pattern rather than putting SQLite calls in services.

Add these files:

```text
src/RigMonitor.Core/Database/
|-- DatabaseDriverBase.cs
|-- DatabaseDriverFactory.cs
|-- DatabaseSettings.cs
|-- DatabaseTypeEnum.cs
|-- Interfaces/
|   `-- ITelemetryHistoryMethods.cs
`-- Sqlite/
    |-- SqliteDatabaseDriver.cs
    |-- Sanitizer.cs
    |-- Converters.cs
    |-- Queries/
    |   `-- SetupQueries.cs
    `-- Implementations/
        `-- TelemetryHistoryMethods.cs
```

`DatabaseDriverBase` should expose a domain-specific `TelemetryHistory` property. Avoid a generic CRUD repository; the references all keep the method groups shaped around product concepts. Start with `Sqlite` only, but keep `DatabaseTypeEnum` extensible for `Mysql`, `Postgres`, and `SqlServer` so later providers do not force API or service rewrites.

Use `Microsoft.Data.Sqlite` in `RigMonitor.Core`. SQLite should enable WAL mode, create tables idempotently, apply migration statements individually, and use explicit read/write concurrency control. The Lattice `SqliteRepository` is the closest local pattern.

Database architecture tasks:

- [x] Add `Microsoft.Data.Sqlite` to `src/RigMonitor.Core/RigMonitor.Core.csproj`.
- [x] Add `DatabaseDriverBase` with a `TelemetryHistory` method group.
- [x] Add `DatabaseDriverFactory` that creates a SQLite driver from settings and rejects unsupported providers with a clear exception.
- [x] Add `ITelemetryHistoryMethods`.
- [x] Add `SqliteDatabaseDriver` with initialization, WAL mode, schema setup, migration execution, query helpers, and disposal.
- [x] Add SQLite `Sanitizer`, `Converters`, `SetupQueries`, and `TelemetryHistoryMethods`.
- [x] Wire database initialization into `RigMonitorServer.CreateAsync` or `StartAsync`. Done in `RigMonitorServer.CreateAsync`.
- [x] Dispose the database driver during server shutdown. Done in `RigMonitorServer.Stop`.

## Schema

Store each collected snapshot as both queryable scalar columns and a full JSON payload. Operators need fast filters and roll-ups, but the dashboard also needs to open the exact sample that was captured.

Primary table:

```sql
CREATE TABLE IF NOT EXISTS telemetry_samples (
    id TEXT PRIMARY KEY,
    hostname TEXT NOT NULL,
    collectedutc TEXT NOT NULL,
    persistedutc TEXT NOT NULL,
    hostplatform TEXT NOT NULL,
    nvidiaavailable INTEGER NOT NULL,
    ollamaavailable INTEGER NOT NULL,
    vllmavailable INTEGER NOT NULL,
    utilyzeavailable INTEGER NOT NULL,
    cpuutilizationpercent REAL,
    logicalcorecount INTEGER,
    memorytotalbytes INTEGER,
    memoryusedbytes INTEGER,
    memoryavailablebytes INTEGER,
    memoryutilizationpercent REAL,
    networkreceivebytespersecond REAL,
    networktransmitbytespersecond REAL,
    diskreadoperationspersecond REAL,
    diskwriteoperationspersecond REAL,
    diskreadqueuedepth REAL,
    diskwritequeuedepth REAL,
    gpudevicecount INTEGER,
    gpuaverageutilizationpercent REAL,
    gpuaveragememoryutilizationpercent REAL,
    gpuaveragetemperaturecelsius REAL,
    gputotalpowerusagewatts REAL,
    ollamaavailablemodelcount INTEGER,
    ollamaloadedmodelcount INTEGER,
    vllmrunningrequests REAL,
    vllmwaitingrequests REAL,
    vllmgpucacheusagepercent REAL,
    utilyzedevicecount INTEGER,
    snapshotjson TEXT NOT NULL
);
```

Per-device GPU table:

```sql
CREATE TABLE IF NOT EXISTS telemetry_gpu_samples (
    id TEXT PRIMARY KEY,
    sampleid TEXT NOT NULL,
    hostname TEXT NOT NULL,
    collectedutc TEXT NOT NULL,
    deviceindex INTEGER NOT NULL,
    uuid TEXT,
    busid TEXT,
    model TEXT,
    driverversion TEXT,
    migprofile TEXT,
    gpuutilizationpercent REAL,
    memoryusedmegabytes REAL,
    memoryfreemegabytes REAL,
    memorytotalmegabytes REAL,
    memoryutilizationpercent REAL,
    temperaturecelsius REAL,
    powerusagewatts REAL,
    smclockmhz REAL,
    memoryclockmhz REAL,
    xiderrors INTEGER,
    FOREIGN KEY (sampleid) REFERENCES telemetry_samples(id) ON DELETE CASCADE
);
```

Indexes should cover `collectedutc`, common range filters, and roll-up dimensions:

- [x] `idx_telemetry_samples_collectedutc`
- [x] `idx_telemetry_samples_cpu_collectedutc`
- [x] `idx_telemetry_samples_memory_collectedutc`
- [x] `idx_telemetry_samples_gpu_collectedutc`
- [x] `idx_telemetry_gpu_samples_sampleid`
- [x] `idx_telemetry_gpu_samples_uuid_collectedutc`
- [x] `idx_telemetry_gpu_samples_model_collectedutc`

Network interfaces, disk volumes, Ollama models, and raw vLLM metrics can remain inside `snapshotjson` for the first implementation. Add separate child tables later only when filtering or roll-ups require them.

Schema completion gates:

- [x] `telemetry_samples` table stores scalar roll-up fields and `snapshotjson`.
- [x] `telemetry_gpu_samples` table stores one row per GPU device per sample.
- [x] Foreign key cascade deletes GPU rows when a sample is deleted.
- [x] Schema setup is idempotent on a brand-new and existing database.
- [x] Migration statements are safe to run repeatedly.

## Models and Contracts

Add shared pagination models:

- [x] `EnumerationOrderEnum`
- [x] `EnumerationQuery`
- [x] `EnumerationResult<T>`

Use the AssistantHub shape: `MaxResults`, `ContinuationToken`, `Ordering`, `StartUtc`, `EndUtc`, `Success`, `TotalRecords`, `RecordsRemaining`, `EndOfResults`, `Objects`, and `TotalMs`. Keep `MaxResults` bounded.

Add telemetry-history models:

- [x] `TelemetrySampleRecord`: lightweight row for grids and enumeration.
- [x] `TelemetrySampleDetail`: sample metadata plus deserialized `TelemetrySnapshot`.
- [x] `TelemetryHistorySearchFilter`: time range, platform, section availability, GPU UUID/model, metric min/max filters, page/page size.
- [x] `TelemetryHistorySearchResult`: request-history style `Data`, `Page`, `PageSize`, `TotalCount`, `TotalPages`.
- [x] `TelemetryRollupRequest`: `StartUtc`, `EndUtc`, `BucketMinutes`, optional metric/device filters, and `IncludeEmptyBuckets`.
- [x] `TelemetryRollupResult`: range metadata, total sample count, interval, and buckets.
- [x] `TelemetryRollupBucket`: `BucketStartUtc`, `BucketEndUtc`, `SampleCount`, averages/min/max for CPU, memory, network, disk, GPU, Ollama, vLLM, and Utilyze scalar fields.
- [x] `TelemetryPersistenceStatus`: worker/database status returned by the status API.

Add an `IdGenerator` helper and constants for `tel_` sample IDs and `tgd_` GPU sample IDs. If PrettyId is adopted as required by the Agents reference, centralize it there rather than scattering GUID creation.

Model completion gates:

- [x] Public models have XML documentation.
- [x] Numeric settings and request fields are clamped or validated.
- [x] JSON remains camelCase and enum values remain string values.
- [x] Fixed request/response bodies use typed models, not `JsonElement`.

## Repository Interface

`ITelemetryHistoryMethods` should be the storage boundary:

```csharp
Task<TelemetrySampleDetail> CreateAsync(TelemetrySnapshot snapshot, CancellationToken token = default);
Task<TelemetrySampleRecord?> ReadAsync(string id, CancellationToken token = default);
Task<TelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token = default);
Task<EnumerationResult<TelemetrySampleRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);
Task<TelemetryHistorySearchResult> SearchAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default);
Task<TelemetryRollupResult> RollupAsync(TelemetryRollupRequest request, CancellationToken token = default);
Task<bool> DeleteAsync(string id, CancellationToken token = default);
Task<long> DeleteBulkAsync(TelemetryHistorySearchFilter filter, CancellationToken token = default);
Task<long> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken token = default);
```

The SQLite implementation should use typed converters for timestamps, booleans, nullable numbers, and JSON. Fixed contracts must deserialize into model classes, not `JsonElement` walking.

Repository method tasks:

- [x] `CreateAsync` persists scalar fields, full snapshot JSON, and GPU child rows.
- [x] `ReadAsync` returns lightweight metadata without hydrating the full snapshot.
- [x] `ReadDetailAsync` returns the full sample with deserialized `TelemetrySnapshot`.
- [x] `EnumerateAsync` implements `EnumerationQuery` ordering, total counts, continuation token, and date range.
- [x] `SearchAsync` implements request-history style paging and all supported filters.
- [x] `RollupAsync` implements bucketized aggregate queries and empty bucket filling.
- [x] `DeleteAsync` deletes one sample and child GPU rows.
- [x] `DeleteBulkAsync` deletes all samples matching a filter.
- [x] `DeleteOlderThanAsync` enforces retention pruning.

## Background Services

Add a `TelemetryPersistenceService` in `src/RigMonitor.Server/Services` or `src/RigMonitor.Telemetry/Services`, depending on final ownership. Server ownership is acceptable because it coordinates settings, database, logging, and lifecycle.

Responsibilities:

- [x] Start after `ITelemetryService.WarmupAsync`. Done through `RigMonitorServer.CreateAsync` and `StartAsync`.
- [x] Use `PeriodicTimer` and `Settings.Persistence.CollectionIntervalMs`. Done in `TelemetryPersistenceService`.
- [x] Collect a full snapshot with the same section-selection defaults as `/v1/telemetry`. Done with `TelemetryRequestOptions.All()`.
- [x] Persist through `Database.TelemetryHistory.CreateAsync`. Done in `TelemetryPersistenceService`.
- [x] Skip overlapping collections if a previous interval is still running. Done with `Interlocked` guard.
- [x] Log and swallow per-iteration failures so telemetry collection problems do not stop the server. Done in collection and retention loops.
- [x] Accept a `TimeProvider` for deterministic tests. Done in `TelemetryPersistenceService`.
- [x] Expose status for diagnostics: enabled, last attempt, last success, last error, next scheduled collection, total persisted count if cheap. Done except total count is intentionally omitted because it is not cheap without an extra count query.

Retention can live in the same service or a small `TelemetryRetentionService`. It should run once at startup and then every `PruneIntervalMinutes`, deleting rows older than `UtcNow - RetentionDays`. Deleting a parent sample must cascade to GPU sample rows.

Wire startup and shutdown through `RigMonitorServer`:

- [x] Create database driver through `DatabaseDriverFactory`.
- [x] Initialize database before starting the web server.
- [x] Construct persistence service when `Settings.Persistence.Enabled`. Implemented as always constructed; the service no-ops when disabled so status APIs still work.
- [x] Start the persistence loop when the server starts.
- [x] Cancel and await background loops in `Stop`.
- [x] Dispose database connections.

## REST API

Keep routes under the existing `/v1` API version.

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/v1/telemetry/history/enumerate` | Returns `EnumerationResult<TelemetrySampleRecord>` using `EnumerationQuery`. |
| `POST` | `/v1/telemetry/history/search` | Returns request-history style paged search results with richer metric filters. |
| `GET` | `/v1/telemetry/history/{id}` | Returns `TelemetrySampleDetail`, including the original snapshot payload. |
| `POST` | `/v1/telemetry/history/rollups` | Returns bucketized average/min/max telemetry over a time range. |
| `GET` | `/v1/telemetry/history/status` | Returns persistence worker/database status. |
| `DELETE` | `/v1/telemetry/history/{id}` | Deletes one sample. |
| `DELETE` | `/v1/telemetry/history` | Deletes samples matching a filter body. |

Route handlers should live in a new `TelemetryHistoryRoutes` registrar. Use typed request/response models and existing `HttpResponder.WriteJsonAsync`. Add OpenAPI metadata for every route so `/openapi.json` and the dashboard API view know about the new surface.

API route tasks:

- [x] Add `TelemetryHistoryRoutes`.
- [x] Register routes in `RigMonitorServer.ConfigureRoutes`.
- [x] Add typed body parsing for enumerate, search, roll-up, and bulk delete requests.
- [x] Return `400` with a useful message for invalid ranges, invalid bucket sizes, and malformed bodies.
- [x] Return `404` for missing sample IDs.
- [x] Add OpenAPI metadata for every route.
- [x] Confirm routes appear in `/openapi.json` and the dashboard API page. Runtime smoke verified `/openapi.json` history paths and `/dashboard/analytics` returned 200.

Roll-up behavior needs to be explicit:

- [x] `StartUtc`, `EndUtc`, and `BucketMinutes` define authoritative bucket boundaries.
- [x] If `EndUtc < StartUtc`, return `400`.
- [x] Clamp `BucketMinutes` to a safe range such as `1..1440`.
- [x] Return empty buckets when `IncludeEmptyBuckets` is true so charts do not have to invent missing time.
- [~] Average only non-null values; include per-metric sample counts where nulls are possible. SQLite averages skip null values; per-metric counts still need either model fields or explicit deferral.
- [x] Verify "average telemetry for 05:00 to 06:00" works with one 60-minute bucket and with smaller trend buckets.

## Dashboard

Add a top-level dashboard route and nav entry:

- [x] Path: `/dashboard/analytics`
- [x] View: `dashboard/src/views/TelemetryAnalyticsView.jsx`
- [x] API functions: `enumerateTelemetryHistory`, `searchTelemetryHistory`, `getTelemetrySample`, `rollupTelemetryHistory`, `getTelemetryHistoryStatus`

The analytics page should borrow the Lattice request-history interaction model but use telemetry concepts:

- [x] Quick ranges: last hour, day, week, month.
- [x] Bucket controls: minute, 15 minutes, hour, 6 hours, day.
- [x] Metric selectors for CPU, memory, GPU utilization, GPU memory, GPU temperature, power, network receive/transmit, disk read/write, Ollama model counts, vLLM running/waiting requests, vLLM GPU cache, and Utilyze device metrics.
- [x] Filters for time range, host platform, GPU UUID/model, availability flags, and metric thresholds.
- [x] Summary cards for sample count, average CPU, average memory, average GPU utilization, average GPU temperature, and latest persisted sample.
- [x] Hand-rolled SVG charts, no charting library.
- [x] Paginated table with collected time, key metrics, platform, GPU count, and row actions.
- [~] Detail modal that shows summary fields first and the full `TelemetrySnapshot` JSON through the existing `JsonModal` pattern. Implemented full sample detail JSON through `JsonModal`; summary-first modal layout is still open.
- [x] Empty, loading, error, and disabled-persistence states.

Follow the existing dashboard architecture and i18n foundation:

- [x] Add all strings to `dashboard/src/i18n/resources.js`; no raw operator-facing English in JSX.
- [x] Use `dashboard/src/i18n/formatters.js` for dates, numbers, percentages, bytes, and durations.
- [~] Make filters and tables responsive at desktop, tablet, and mobile widths. CSS is responsive; browser viewport verification is still pending.
- [x] Avoid card nesting; use the existing `surface`, table, modal, and toolbar patterns.
- [x] Keep the page operational and dense rather than marketing-styled.

## Documentation and Product Surface

Update these files as part of the implementation:

- [x] `README.md`: feature overview, default settings, data location, retention behavior, dashboard analytics page.
- [x] `REST_API.md`: all new history, search, roll-up, status, and delete endpoints with examples.
- [x] `CHANGELOG.md`: persistence, analytics, retention, and API additions.
- [x] `RigMonitor.postman_collection.json`: requests for enumerate, search, sample detail, roll-up, status, and delete.
- [x] `BARE_METAL_DEPLOYMENT.md`: persistence directory, file permissions, service account write access, retention notes.
- [x] `docker/compose.yaml` and `docker/Dockerfile` if needed: make sure `data/` is mounted and SQLite WAL/SHM files are not lost unexpectedly.
- [x] `.gitignore`: ignore `*.db`, `*.db-wal`, `*.db-shm`, and local telemetry data files while keeping `data/.gitkeep`.

`rigmonitor.json` is currently deleted in this working tree, so do not recreate it accidentally unless the implementation task explicitly asks for a default checked-in settings file.

## Tests

Backfill tests at the same depth as the product surface.

Backend tests:

- [x] Settings clamp/default tests for `PersistenceSettings` and `DatabaseSettings`. Covered by `PersistenceTests`.
- [x] SQLite initialization test creates tables/indexes and can run twice idempotently. Covered by `PersistenceTests`.
- [x] Create/read/detail tests preserve scalar fields and full snapshot JSON. Covered by `PersistenceTests`.
- [x] Enumeration tests cover max results, continuation token, ordering, total records, records remaining, and date filters.
- [x] Search tests cover time range, GPU filters, availability flags, and metric thresholds.
- [~] Roll-up tests cover one-hour average, empty bucket filling, per-metric counts, invalid ranges, and bucket-size clamps. One-hour average, empty buckets, invalid range, and bucket clamp covered; per-metric counts remain open because the public contract does not expose them yet.
- [~] Retention tests use `ManualTimeProvider` and verify parent/child rows are removed. Retention pruning is covered with `ManualTimeProvider`; explicit child-row checks remain open.
- [~] Background service tests verify cadence, no overlapping writes, cancellation, and failure isolation. Immediate collection, cancellation, status, and startup pruning are covered; overlap/failure isolation tests remain open.
- [~] Route tests verify status codes and JSON contracts for happy paths and invalid requests. Runtime smoke verifies route registration/OpenAPI/dashboard status; dedicated route unit tests remain open.
- [x] Serialization tests cover camelCase JSON and enum string values.

Frontend checks:

- [x] Add focused unit coverage if a JS test runner is introduced; otherwise at minimum run `npm run build`. Ran `npm.cmd run build`.
- [ ] Manually verify the analytics page at desktop, tablet, and mobile widths with realistic, empty, loading, and error states.
- [x] Confirm all new strings come from i18n resources and all metric formatting uses existing formatter helpers.

Project-level verification:

- [x] `dotnet build src/RigMonitor.sln`
- [x] `dotnet test src/Test.Xunit`
- [x] `dotnet test src/Test.Nunit`
- [x] `dotnet test src/Test.Automated`
- [x] `npm run build` from `dashboard`. Ran via `npm.cmd run build` because PowerShell blocked `npm.ps1`.
- [x] Validate `RigMonitor.postman_collection.json` parses as JSON.

## Implementation Phases

1. [x] Add settings, database contracts, models, and SQLite schema.
2. [~] Implement SQLite telemetry-history methods and shared database tests.
3. [x] Add background collection and retention lifecycle wiring.
4. [~] Add REST routes, request parsers, OpenAPI metadata, and route tests.
5. [~] Build the dashboard analytics view, API helpers, styles, and i18n entries.
6. [x] Update README, REST API docs, Postman, deployment docs, changelog, and ignore rules.
7. [~] Run the full verification matrix and fix any warnings or dashboard build failures. Build/tests/Postman JSON/runtime smoke pass; manual viewport checks remain open.

## Definition of Done

- [x] A clean install creates a default settings file with `Persistence.RetentionDays = 30`.
- [~] When persistence is enabled, snapshots are collected on the configured cadence and survive server restart. Immediate collection and SQLite persistence are tested; explicit restart survival smoke remains open.
- [x] Retention pruning removes samples older than the configured retention window.
- [x] SQLite is hidden behind database interfaces; services and routes do not depend on SQLite types.
- [x] Enumerate, search, detail, roll-up, status, and delete APIs are documented, tested, and visible in OpenAPI.
- [~] The dashboard analytics page lets an operator filter, chart, page through, and inspect historical telemetry samples. Functional build and runtime serving are verified; browser viewport/manual UX pass remains open.
- [x] README, REST API docs, Postman, changelog, deployment docs, Docker notes, and ignore rules are updated.
- [x] Backend tests, dashboard build, and Postman JSON validation pass.
- [x] Existing live `/v1/telemetry` behavior remains backward compatible.

The riskiest choices are schema shape and API contract shape. Resolve those first. Once scalar roll-up columns and public route names are stable, the worker, dashboard, docs, and tests can move without churn.
