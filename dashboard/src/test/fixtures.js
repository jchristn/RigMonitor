function status(statusCode, supported = true) {
  return {
    requested: true,
    supported,
    statusCode,
    freshness: { status: statusCode === 'ok' ? 'fresh' : 'notApplicable', staleAfterMs: 15000 },
    message: `${statusCode} message`,
  }
}

export const nvidiaSection = {
  source: 'dcgmExporter',
  endpoint: 'http://localhost:9400/metrics',
  devices: [
    {
      ordinal: 0,
      deviceIndex: 0,
      vendor: 'nvidia',
      source: 'dcgmExporter',
      uuid: 'GPU-1',
      model: 'NVIDIA L40S',
      busId: '0000:01:00.0',
      xidErrors: 2,
      unifiedMemory: false,
      metrics: {
        gpuUtilizationPercent: 73,
        memoryUsedMegabytes: 12288,
        memoryTotalMegabytes: 46080,
        memoryUtilizationPercent: 26.7,
        memoryShared: false,
        temperatureCelsius: 61,
        powerUsageWatts: 250,
        smClockMHz: 1980,
      },
    },
  ],
}

export const amdSection = {
  source: 'amdSmiLibrary',
  gapFillSource: 'amdSysfs',
  libraryVersion: '6.4.0',
  endpoint: '/opt/rocm/lib/libamd_smi.so',
  devices: [
    {
      ordinal: 1,
      deviceIndex: 0,
      vendor: 'amd',
      source: 'amdSmiLibrary',
      uuid: 'amd-0',
      model: 'AMD Instinct MI300X',
      partitionId: '0',
      hotspotTemperatureCelsius: 75,
      gttUsedMegabytes: 1024,
      gttTotalMegabytes: 65536,
      fieldSources: { utilization: 'amdSmiLibrary', power: 'amdSysfs' },
      unifiedMemory: false,
      metrics: {
        gpuUtilizationPercent: 91,
        memoryUsedMegabytes: 65536,
        memoryTotalMegabytes: 196608,
        memoryUtilizationPercent: 33.3,
        temperatureCelsius: 70,
        powerUsageWatts: 612.5,
      },
    },
  ],
}

export const appleSection = {
  source: 'appleNative',
  chip: 'Apple M5 Max',
  gpuCoreCount: 40,
  metalFamily: 'apple10',
  thermalState: 'serious',
  temperatureSource: 'socDie',
  hostMemoryTotalMegabytes: 131072,
  recommendedMaxWorkingSetMegabytes: 110100,
  features: ['ioKit', 'metal', 'ioReport'],
  devices: [
    {
      ordinal: 2,
      deviceIndex: 0,
      vendor: 'apple',
      source: 'appleNative',
      uuid: 'AGX/0',
      model: 'Apple M5 Max',
      unifiedMemory: true,
      rendererUtilizationPercent: 14,
      tilerUtilizationPercent: 11,
      metrics: {
        gpuUtilizationPercent: 14,
        memoryUsedMegabytes: 1684,
        memoryTotalMegabytes: 131072,
        memoryUtilizationPercent: 1.3,
        memoryShared: true,
        powerUsageWatts: 0.3,
        smClockMHz: 338,
      },
    },
  ],
}

export const qualcommSection = {
  source: 'drmMsm',
  endpoint: '/sys',
  devices: [
    {
      ordinal: 3,
      deviceIndex: 0,
      vendor: 'qualcomm',
      source: 'drmMsm',
      uuid: '3d00000.gpu',
      model: 'Qualcomm Adreno GPU (chip 0x43050c01)',
      utilizationScope: 'ownProcesses',
      engineUtilization: {},
      currentFrequencyMHz: 680,
      maxFrequencyMHz: 1250,
      fieldSources: { utilization: 'drmMsm' },
      unifiedMemory: true,
      metrics: {
        gpuUtilizationPercent: 50,
        memoryUsedMegabytes: 3,
        memoryTotalMegabytes: 32768,
        memoryShared: true,
        temperatureCelsius: 53,
        smClockMHz: 680,
      },
    },
  ],
}

export function snapshot(sections = {}) {
  const vendors = ['nvidia', 'amd', 'apple', 'qualcomm']
  const devices = vendors.flatMap((vendor) => sections[vendor]?.devices || [])
  const collection = {}
  vendors.forEach((vendor) => {
    collection[vendor] = sections[vendor] ? status('ok') : status('unsupported', false)
  })

  return {
    collectedUtc: '2026-11-12T20:00:00Z',
    memory: { totalBytes: 32 * 1024 * 1024 * 1024 },
    ...sections,
    gpu: devices.length > 0 ? { vendor: 'mixed', vendors: [], devices } : undefined,
    collection: { ...collection, gpu: status(devices.length > 0 ? 'ok' : 'unsupported', devices.length > 0) },
  }
}
