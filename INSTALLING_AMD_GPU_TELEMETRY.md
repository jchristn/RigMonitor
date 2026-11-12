# AMD GPU Telemetry

RigMonitor reports AMD GPUs in the `amd` section of `/v1/telemetry` and in the AMD section of the dashboard. On Linux it reads AMD GPUs in-process. It does not run `amd-smi` or `rocm-smi` and parse their output.

For NVIDIA see [INSTALLING_DCGM.md](./INSTALLING_DCGM.md). For Apple see [APPLE_GPU_TELEMETRY.md](./APPLE_GPU_TELEMETRY.md). For Qualcomm see [QUALCOMM_GPU_TELEMETRY.md](./QUALCOMM_GPU_TELEMETRY.md).

## Sources

| Source | `source` value | What it is | Used when |
|--------|----------------|------------|-----------|
| ROCm AMD SMI library | `amdSmiLibrary` | `libamd_smi.so`, loaded into RigMonitor and called directly | ROCm is installed and the library finds at least one GPU |
| amdgpu sysfs | `amdSysfs` | Files the `amdgpu` kernel driver publishes under `/sys/class/drm/card*/device` | No ROCm, or as gap-fill for the library |
| AMD Device Metrics Exporter | `amdMetricsExporter` | Prometheus text over HTTP, default `http://localhost:5000/metrics` | Neither of the above works (for example in a container), or chosen explicitly |
| Windows graphics kernel | `windowsGraphicsKernel` | DXGI, PDH, and D3DKMT on Windows | Always on Windows (see below) |

`Telemetry.AmdSource` selects the behavior:

- `auto` (default): library first; if it is unavailable, sysfs; if no `amdgpu` cards exist, the exporter.
- `native`: library only.
- `sysfs`: sysfs only.
- `exporter`: exporter only.

### Gap-fill

When the library is the primary source and `Telemetry.AmdSysfsGapFillEnabled` is `true` (the default), RigMonitor also reads sysfs. It matches devices by PCI address and fills only fields the library could not provide. A common example is power on parts where the library reports power as unsupported. The section reports `gapFillSource: "amdSysfs"`, and each device's `fieldSources` shows which source supplied each field. Devices are never counted twice.

If the library fails while sampling, that sample falls back to sysfs.

## What is reported

- Utilization (graphics activity), memory-controller and multimedia activity
- VRAM used/total, and GTT used/total (GPU-accessible system memory)
- Edge, hotspot (junction), and memory temperatures
- Power, graphics clock, memory clock
- Correctable and uncorrectable ECC counts
- Compute and memory partition modes (for example SPX/CPX and NPS1/NPS4); partitions appear as separate devices with the exporter

APUs such as Ryzen AI Max (Strix Halo), Phoenix, and MI300A are flagged `unifiedMemory: true`. For APUs, the common memory fields combine the VRAM carve-out and GTT (`memorySource: "vramPlusGtt"`), because that is the memory the GPU can actually use for models.

## Ubuntu 24.04

1. Install the `amdgpu` driver (included in the Ubuntu kernel for most GPUs). sysfs telemetry works with only this.
2. For the richest data, install ROCm so `libamd_smi.so` is present (normally `/opt/rocm/lib`). Follow AMD's ROCm installation guide for your GPU.
3. Give the RigMonitor service user access to the GPU device nodes:

   ```bash
   sudo usermod -aG render,video "$USER"
   ```

   Log out and back in (or restart the service) for the group change to apply. The systemd unit installed by `scripts/linux/install-at-startup.sh` sets `SupplementaryGroups=render video`.

4. Restart RigMonitor. GPU sources are detected at startup.

If ROCm is installed outside `/opt/rocm`, set `Telemetry.AmdSmiLibraryPath` or `ROCM_PATH`. When starting RigMonitor manually with ROCm in a non-default location, also add its `lib` directory to `LD_LIBRARY_PATH` so the library's own dependencies resolve.

## Verify

```bash
ls /opt/rocm/lib/libamd_smi.so*
cat /sys/class/drm/card*/device/gpu_busy_percent
curl -s http://127.0.0.1:9990/v1/capabilities | python3 -m json.tool | grep -A3 '"amd'
curl -s 'http://127.0.0.1:9990/v1/telemetry?amd' | python3 -m json.tool
```

The startup log contains a line such as:

```text
AMD GPU available: true (source: AmdSmiLibrary, lib 6.4.0, gap-fill: amdSysfs)
```

## Containers

The Docker image does not include ROCm. Inside a container, RigMonitor uses sysfs (visible by default, read-only) or the AMD Device Metrics Exporter running on the host. To reach a host exporter from the bundled compose file, set `Telemetry.AmdMetricsExporterUrl` to `http://host.docker.internal:5000/metrics` (with `extra_hosts: ["host.docker.internal:host-gateway"]`) and `Telemetry.AmdSource` to `exporter`.

## Windows

On Windows, AMD adapters are read through the vendor-neutral Windows graphics kernel backend (the same backend used for Qualcomm): utilization per engine from PDH, memory usage from PDH and budgets from DXGI, and temperature and clocks from D3DKMT when the driver publishes them. Power is not available there. Set `Telemetry.WindowsGenericGpuEnabled` to `false` to turn this off.

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| `libamd_smi.so was not found` in `gpuProviders` | ROCm is not installed, or it is in a non-default location; set `Telemetry.AmdSmiLibraryPath` |
| `amdsmi_init failed` | The service user cannot open `/dev/kfd` or `/dev/dri/*`; add it to `render` and `video` |
| Source is `amdSysfs` on a ROCm host | Library initialization failed; see the startup log and `gpuProviders[].detail` |
| Temperatures or power missing | The ASIC does not expose that sensor; check `hwmon` under the card's device directory |
| Memory looks small on an APU | Check `gttTotalMegabytes`; the BIOS VRAM carve-out is often small |
