# Installing DCGM for RigMonitor

RigMonitor does not query NVIDIA DCGM hostengine directly. It reads NVIDIA GPU telemetry from the Prometheus endpoint exposed by NVIDIA DCGM exporter.

By default RigMonitor expects:

```text
http://localhost:9400/metrics
```

The endpoint must be reachable when RigMonitor starts. If you install or repair DCGM exporter after RigMonitor is already running, restart RigMonitor.

## Components

- NVIDIA driver: provides GPU device support.
- NVIDIA DCGM hostengine: the DCGM service, commonly exposed by `nvidia-dcgm.service` and `nv-hostengine` on port `5555`.
- NVIDIA DCGM exporter: the Prometheus exporter RigMonitor actually reads, commonly exposed by `nvidia-dcgm-exporter.service` on port `9400`.

DCGM hostengine can be healthy while RigMonitor still reports DCGM unavailable. That usually means DCGM exporter is not installed, not running, not listening on `9400`, or not emitting the expected metric names.

## Ubuntu 24.04 Quick Start

Install the exporter package from the NVIDIA package repository configured for your host:

```bash
sudo apt update
apt-cache policy datacenter-gpu-manager-exporter
sudo apt install -y datacenter-gpu-manager-exporter
```

If `apt-cache policy` cannot find the package, configure NVIDIA's current Ubuntu repository for your driver/DCGM stack, then rerun the commands above.

Confirm the installed binary and service name:

```bash
command -v dcgm-exporter
systemctl list-unit-files '*dcgm*exporter*'
```

On many Ubuntu systems the systemd unit is named:

```text
nvidia-dcgm-exporter.service
```

Start and enable it:

```bash
sudo systemctl enable --now nvidia-dcgm-exporter.service
systemctl status nvidia-dcgm-exporter.service --no-pager
```

Verify that the exporter is listening and emitting metrics:

```bash
ss -ltnp | grep ':9400'
curl -fsS http://127.0.0.1:9400/metrics | grep -E 'DCGM_FI_DEV_(GPU_UTIL|FB_USED|FB_FREE|FB_TOTAL|GPU_TEMP)' | head
```

Expected output includes Prometheus metric names similar to:

```text
DCGM_FI_DEV_GPU_TEMP
DCGM_FI_DEV_GPU_UTIL
DCGM_FI_DEV_FB_USED
DCGM_FI_DEV_FB_FREE
```

Restart RigMonitor after the exporter works, then verify capabilities:

```bash
curl -fsS http://127.0.0.1:9990/v1/capabilities
curl -fsS 'http://127.0.0.1:9990/v1/telemetry?gpu'
```

`/v1/capabilities` should report:

```json
{
  "nvidiaAvailable": true
}
```

## Existing DCGM Hostengine

Some DGX-style systems run `nv-hostengine` through `nvidia-dcgm.service` before the exporter is installed.

Check hostengine status:

```bash
systemctl status nvidia-dcgm.service --no-pager
```

If the exporter service fails because hostengine is already running, test the exporter manually against the existing hostengine:

```bash
sudo dcgm-exporter -r localhost:5555 -a :9400 -f /etc/dcgm-exporter/default-counters.csv
```

If that works, adjust the systemd unit or environment file for your installed `nvidia-dcgm-exporter.service` so it connects to the existing hostengine instead of trying to create a conflicting embedded hostengine.

Inspect the unit before changing it:

```bash
systemctl cat nvidia-dcgm-exporter.service
```

## RigMonitor Configuration

The relevant setting is `Telemetry.DcgmExporterUrl` in `rigmonitor.json`:

```json
{
  "telemetry": {
    "dcgmExporterUrl": "http://localhost:9400/metrics"
  }
}
```

For a bare-metal RigMonitor process running on the same host as DCGM exporter, `localhost` is usually correct.

For containerized deployments, `localhost` inside the RigMonitor container is not the host. Use host networking or set `Telemetry.DcgmExporterUrl` to an exporter URL reachable from inside the container.

## Troubleshooting

RigMonitor reports `nvidiaAvailable:false` when the startup probe cannot fetch exporter metrics or cannot find one of these metric names:

```text
DCGM_FI_DEV_GPU_UTIL
DCGM_FI_DEV_FB_USED
DCGM_FI_DEV_FB_FREE
DCGM_FI_DEV_GPU_TEMP
```

Use these checks on the RigMonitor host:

```bash
curl -fsS http://127.0.0.1:9400/metrics | grep -E 'DCGM_FI_DEV_(GPU_UTIL|FB_USED|FB_FREE|FB_TOTAL|GPU_TEMP)' | head
ss -ltnp | grep ':9400'
systemctl status nvidia-dcgm.service nvidia-dcgm-exporter.service --no-pager
journalctl -u nvidia-dcgm-exporter.service -n 100 --no-pager
curl -fsS http://127.0.0.1:9990/v1/capabilities
```

Common causes:

- `nvidia-dcgm.service` is running, but `nvidia-dcgm-exporter.service` is missing or stopped.
- The installed exporter unit has a different name; find it with `systemctl list-unit-files '*dcgm*exporter*'`.
- The exporter is running but not listening on `9400`.
- The exporter is listening but the configured counters file does not emit the metric names RigMonitor probes for.
- RigMonitor was started before the exporter was reachable and needs a restart.

## Upstream References

- NVIDIA DCGM exporter documentation: <https://docs.nvidia.com/datacenter/dcgm/latest/gpu-telemetry/dcgm-exporter.html>
- NVIDIA DCGM exporter source and examples: <https://github.com/NVIDIA/dcgm-exporter>
