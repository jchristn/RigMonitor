# Bare-Metal Deployment on Ubuntu

This guide deploys RigMonitor directly on an Ubuntu host without Docker. It assumes Ubuntu 24.04 LTS or newer, systemd, and a host that can reach any optional telemetry services you enable.

RigMonitor is a .NET 10 application with a React/Vite dashboard. The dashboard must be built before the .NET server is built or published, because the server project copies `dashboard/dist` into its `wwwroot` output.

## Layout

Recommended production layout:

```text
/opt/rigmonitor/                 Published application files
/etc/rigmonitor/rigmonitor.json  Runtime configuration
/var/log/rigmonitor/             Log files
/var/lib/rigmonitor/             Service account home/state directory and telemetry database
```

The examples below install the app under `/opt/rigmonitor` and run it as a dedicated `rigmonitor` user.

## Prerequisites

Install system packages:

```bash
sudo apt update
sudo apt install -y git curl ca-certificates nodejs npm
```

Install the .NET 10 SDK on the build host. On Ubuntu 24.04 and newer, .NET 10 is available from Ubuntu package feeds on supported architectures:

```bash
sudo apt update
sudo apt install -y dotnet-sdk-10.0
dotnet --info
```

If your Ubuntu image does not offer `dotnet-sdk-10.0`, use the current Microsoft/Ubuntu guidance:

- <https://learn.microsoft.com/dotnet/core/install/linux-ubuntu>
- <https://documentation.ubuntu.com/ubuntu-for-developers/reference/availability/dotnet/>

You only need the SDK on machines that build or publish RigMonitor. A runtime-only deployment can use the .NET 10 runtime, but using the SDK is simpler for first installs.

## NVIDIA GPU Telemetry Prerequisite

RigMonitor reads NVIDIA telemetry from NVIDIA DCGM exporter, not directly from `nvidia-smi` or `nv-hostengine`.

For NVIDIA GPU telemetry on the same host, install NVIDIA drivers first, then install DCGM exporter:

```bash
sudo apt update
apt-cache policy datacenter-gpu-manager-exporter
sudo apt install -y datacenter-gpu-manager-exporter
```

If `apt-cache policy datacenter-gpu-manager-exporter` does not find a package, configure NVIDIA's Ubuntu package repository for your driver/DCGM stack, then rerun the install command. The detailed DCGM guide includes troubleshooting notes for this case: [INSTALLING_DCGM.md](./INSTALLING_DCGM.md).

Confirm the exporter binary and systemd unit name:

```bash
command -v dcgm-exporter
systemctl list-unit-files '*dcgm*exporter*'
```

On many Ubuntu systems the unit is named `nvidia-dcgm-exporter.service`. Enable and start it:

```bash
sudo systemctl enable --now nvidia-dcgm-exporter.service
systemctl status nvidia-dcgm-exporter.service --no-pager
```

If your host uses a different exporter unit name, substitute that unit name in the `systemctl` commands and in the optional systemd ordering lines for RigMonitor below.

Verify exporter metrics before starting RigMonitor:

```bash
ss -ltnp | grep ':9400'
curl -fsS http://127.0.0.1:9400/metrics | grep -E 'DCGM_FI_DEV_(GPU_UTIL|FB_USED|FB_FREE|FB_TOTAL|GPU_TEMP)' | head
```

If `nvidia-dcgm.service` or `nv-hostengine` is already running and the exporter fails to start, test the exporter against the existing hostengine:

```bash
sudo dcgm-exporter -r localhost:5555 -a :9400 -f /etc/dcgm-exporter/default-counters.csv
```

If that works, adjust your installed `nvidia-dcgm-exporter.service` unit or environment so it connects to the existing hostengine instead of trying to start a conflicting embedded hostengine.

If DCGM exporter is installed or repaired after RigMonitor has already started, restart RigMonitor so capability detection runs again.

Full DCGM exporter setup and troubleshooting is documented in [INSTALLING_DCGM.md](./INSTALLING_DCGM.md).

On GB10 unified-memory systems, framebuffer counters may report zero because GPU memory is shared with system RAM. RigMonitor falls back to host physical memory for GPU RAM display on GB10 and marks that memory as shared.

## Build and Publish

Run these commands from the repository root.

Build the dashboard first:

```bash
cd dashboard
npm ci
npm run build
cd ..
```

Publish the server:

```bash
cd src
dotnet publish RigMonitor.Server/RigMonitor.Server.csproj -c Release -o /tmp/rigmonitor-publish
cd ..
```

Confirm the dashboard bundle was included:

```bash
test -f /tmp/rigmonitor-publish/wwwroot/index.html
find /tmp/rigmonitor-publish/wwwroot/assets -maxdepth 1 -type f | head
```

If `wwwroot/index.html` is missing, rebuild the dashboard with `npm run build`, then rerun `dotnet publish`.

## Install Files

Create the service user and directories:

```bash
sudo useradd --system --home-dir /var/lib/rigmonitor --create-home --shell /usr/sbin/nologin rigmonitor
sudo mkdir -p /opt/rigmonitor /etc/rigmonitor /var/log/rigmonitor /var/lib/rigmonitor
```

Install the published app:

```bash
sudo rsync -a --delete /tmp/rigmonitor-publish/ /opt/rigmonitor/
sudo chown -R root:root /opt/rigmonitor
sudo chmod -R a+rX /opt/rigmonitor
```

Install the configuration file:

```bash
sudo cp rigmonitor.json /etc/rigmonitor/rigmonitor.json
sudo chown root:rigmonitor /etc/rigmonitor/rigmonitor.json
sudo chmod 640 /etc/rigmonitor/rigmonitor.json
```

Create the log directory:

```bash
sudo chown rigmonitor:rigmonitor /var/log/rigmonitor
sudo chmod 750 /var/log/rigmonitor
sudo chown rigmonitor:rigmonitor /var/lib/rigmonitor
sudo chmod 750 /var/lib/rigmonitor
```

Edit `/etc/rigmonitor/rigmonitor.json` for production:

```json
{
  "webserver": {
    "hostname": "0.0.0.0",
    "port": 9990,
    "ssl": false
  },
  "telemetry": {
    "dcgmExporterUrl": "http://127.0.0.1:9400/metrics",
    "ollamaBaseUrl": "http://127.0.0.1:11434",
    "vllmEnabled": false,
    "vllmMetricsUrl": "http://127.0.0.1:8000/metrics",
    "utilyzeEnabled": false,
    "utilyzeLiveUrl": "ws://127.0.0.1:8079/live"
  },
  "logging": {
    "logDirectory": "/var/log/rigmonitor",
    "fileLogging": true,
    "consoleLogging": true,
    "minimumSeverity": "debug"
  },
  "persistence": {
    "enabled": true,
    "collectionIntervalMs": 15000,
    "retentionDays": 30,
    "pruneIntervalMinutes": 60,
    "hostname": "localhost",
    "database": {
      "type": "Sqlite",
      "filename": "/var/lib/rigmonitor/rigmonitor.telemetry.db",
      "logQueries": false
    }
  },
  "dashboard": {
    "enabled": true
  }
}
```

Use `hostname: "127.0.0.1"` if RigMonitor should be reachable only from the local machine or from a reverse proxy on the same host. Use `hostname: "0.0.0.0"` to listen on all interfaces.

`persistence.hostname` is the host label written to every telemetry history row. Leave it null or empty to store `localhost`; set a stable machine name when several RigMonitor instances export data to the same downstream workflow.

SQLite creates the main database file plus `rigmonitor.telemetry.db-wal` and `rigmonitor.telemetry.db-shm` while WAL mode is active. Keep all three files in `/var/lib/rigmonitor` and include them in any backup or volume policy.

## systemd Service

Create `/etc/systemd/system/rigmonitor.service`:

```ini
[Unit]
Description=RigMonitor telemetry daemon
Documentation=https://github.com/jchristn/RigMonitor
After=network-online.target
Wants=network-online.target

# If DCGM exporter is installed on this host, RigMonitor should start after it.
# Keep these commented if your exporter unit has a different name.
# After=nvidia-dcgm-exporter.service
# Wants=nvidia-dcgm-exporter.service

[Service]
Type=simple
User=rigmonitor
Group=rigmonitor
WorkingDirectory=/opt/rigmonitor
ExecStart=/usr/bin/dotnet /opt/rigmonitor/RigMonitor.Server.dll --settings /etc/rigmonitor/rigmonitor.json
Restart=on-failure
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30

NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ReadWritePaths=/var/log/rigmonitor /var/lib/rigmonitor

[Install]
WantedBy=multi-user.target
```

Enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rigmonitor.service
sudo systemctl status rigmonitor.service --no-pager
```

View logs:

```bash
journalctl -u rigmonitor.service -f
tail -f /var/log/rigmonitor/rigmonitor.log*
```

## Runtime State Reset

The published app includes `clean.sh`, but it only cleans runtime files located under the directory where the script lives. The production layout in this guide stores configuration, logs, and database state outside `/opt/rigmonitor`, so use explicit commands when you need a destructive reset.

Stop RigMonitor first:

```bash
sudo systemctl stop rigmonitor.service
```

Remove generated configuration, logs, and SQLite history, including WAL/SHM sidecars:

```bash
sudo rm -f /etc/rigmonitor/rigmonitor.json
sudo rm -f /var/lib/rigmonitor/rigmonitor.telemetry.db*
sudo rm -rf /var/log/rigmonitor/*
```

Reinstall or recreate `/etc/rigmonitor/rigmonitor.json`, then restart the service:

```bash
sudo systemctl start rigmonitor.service
```

## Firewall

If the dashboard/API should be reachable from other machines and the host firewall is enabled, allow the configured port:

```bash
sudo ufw allow 9990/tcp
sudo ufw status
```

If RigMonitor is behind a reverse proxy, keep the service bound to `127.0.0.1` and expose only the proxy.

## Validation

Check the service:

```bash
systemctl is-active rigmonitor.service
curl -fsS http://127.0.0.1:9990/livez
curl -fsS http://127.0.0.1:9990/readyz
curl -fsS http://127.0.0.1:9990/v1/capabilities
curl -fsS http://127.0.0.1:9990/v1/telemetry
curl -fsS http://127.0.0.1:9990/v1/telemetry/history/status
```

Open the dashboard:

```text
http://<host>:9990/dashboard
```

Verify NVIDIA telemetry when expected:

```bash
curl -fsS http://127.0.0.1:9400/metrics | grep -E 'DCGM_FI_DEV_(GPU_UTIL|FB_USED|FB_FREE|FB_TOTAL|GPU_TEMP)' | head
curl -fsS http://127.0.0.1:9990/v1/capabilities | grep -i nvidia
curl -fsS 'http://127.0.0.1:9990/v1/telemetry?gpu'
```

The dashboard should show `GPU RAM` when memory data is available. GB10 systems should show shared GPU RAM.
The analytics page should be reachable at `http://<host>:9990/dashboard/analytics` after at least one persisted sample has been collected.

## Upgrade

From the repository root:

```bash
git pull --ff-only origin main

cd dashboard
npm ci
npm run build
cd ../src
dotnet publish RigMonitor.Server/RigMonitor.Server.csproj -c Release -o /tmp/rigmonitor-publish
cd ..

sudo systemctl stop rigmonitor.service
sudo rsync -a --delete /tmp/rigmonitor-publish/ /opt/rigmonitor/
sudo chown -R root:root /opt/rigmonitor
sudo chmod -R a+rX /opt/rigmonitor
sudo systemctl start rigmonitor.service
sudo systemctl status rigmonitor.service --no-pager
```

If configuration schema changes, merge the new `rigmonitor.json` defaults into `/etc/rigmonitor/rigmonitor.json` before starting the service.

## Troubleshooting

Dashboard still shows old UI:

- Run `git log -1 --oneline` and confirm the expected commit is checked out.
- Run `npm run build` in `dashboard`.
- Rerun `dotnet publish` after the dashboard build.
- Confirm `/opt/rigmonitor/wwwroot/index.html` was updated.
- Hard refresh the browser or clear cached assets.

`nvidiaAvailable` is `false`:

- Confirm DCGM exporter is running: `systemctl status nvidia-dcgm-exporter.service --no-pager`.
- Confirm metrics are reachable from the RigMonitor host: `curl -fsS http://127.0.0.1:9400/metrics | head`.
- Confirm expected metric names are emitted.
- Restart RigMonitor after fixing the exporter.

Service starts but dashboard is unreachable:

- Check bind address in `/etc/rigmonitor/rigmonitor.json`.
- Check the port: `ss -ltnp | grep ':9990'`.
- Check firewall rules: `sudo ufw status`.
- Check logs: `journalctl -u rigmonitor.service -n 100 --no-pager`.

Service cannot write logs:

- Confirm `logging.logDirectory` points to `/var/log/rigmonitor`.
- Confirm ownership: `sudo chown rigmonitor:rigmonitor /var/log/rigmonitor`.

Service cannot write telemetry history:

- Confirm `persistence.database.filename` points under `/var/lib/rigmonitor`.
- Confirm ownership: `sudo chown rigmonitor:rigmonitor /var/lib/rigmonitor`.
- Confirm the service unit includes `/var/lib/rigmonitor` in `ReadWritePaths`.
- Check for the main SQLite file plus WAL/SHM files: `ls -la /var/lib/rigmonitor/rigmonitor.telemetry.db*`.

History grows too large:

- Confirm `persistence.retentionDays` is set to the desired retention window.
- Confirm `persistence.pruneIntervalMinutes` is not set higher than your operational tolerance.
- Check the status endpoint for the configured retention: `curl -fsS http://127.0.0.1:9990/v1/telemetry/history/status`.

Optional telemetry is missing:

- Ollama must be reachable at `telemetry.ollamaBaseUrl` before RigMonitor starts.
- vLLM requires `telemetry.vllmEnabled: true` and a reachable Prometheus metrics endpoint.
- Utilyze requires `telemetry.utilyzeEnabled: true` and a reachable live WebSocket endpoint.
