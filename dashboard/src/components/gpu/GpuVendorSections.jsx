import { Fragment, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { SectionCard } from '../SectionCard'
import {
  formatMegabytes,
  formatMemoryRange,
  formatNumber,
  formatOptional,
  formatPercent,
  hasNumber,
  thermalStateTone,
} from '../../i18n/formatters'
import { UnifiedMemoryBar } from './UnifiedMemoryBar'

const guideBaseUrl = 'https://github.com/jchristn/RigMonitor/blob/main/'

export const gpuVendors = [
  { key: 'nvidia', capability: 'nvidiaAvailable', guide: 'INSTALLING_DCGM.md' },
  { key: 'amd', capability: 'amdAvailable', guide: 'INSTALLING_AMD_GPU_TELEMETRY.md' },
  { key: 'apple', capability: 'appleGpuAvailable', guide: 'APPLE_GPU_TELEMETRY.md' },
  { key: 'qualcomm', capability: 'qualcommAvailable', guide: 'QUALCOMM_GPU_TELEMETRY.md' },
]

function sum(devices, select) {
  let total = 0
  let seen = false
  devices.forEach((device) => {
    const value = select(device)
    if (hasNumber(value)) {
      total += value
      seen = true
    }
  })

  return seen ? total : null
}

function deviceKey(device) {
  return `${device.vendor}-${device.ordinal}-${device.deviceIndex}-${device.uuid}`
}

function SourcePill({ source }) {
  const { t } = useTranslation()
  if (!source) {
    return null
  }

  return <span className="pill">{`${t('labels.source')}: ${t(`sources.${source}`, { defaultValue: source })}`}</span>
}

function DetailList({ items }) {
  const visible = items.filter((item) => item.value != null && item.value !== '' && item.value !== '—')
  if (visible.length < 1) {
    return null
  }

  return (
    <dl className="gpu-detail-grid">
      {visible.map((item) => (
        <div className="gpu-detail-item" key={item.label}>
          <dt>{item.label}</dt>
          <dd>{item.value}</dd>
        </div>
      ))}
    </dl>
  )
}

function fieldSourcesText(fieldSources, t) {
  const entries = Object.entries(fieldSources || {})
  if (entries.length < 1) {
    return null
  }

  return entries.map(([field, source]) => `${field}: ${t(`sources.${source}`, { defaultValue: source })}`).join(', ')
}

function DeviceTable({ columns, devices, details }) {
  const { t } = useTranslation()
  const [expanded, setExpanded] = useState({})

  function toggle(key) {
    setExpanded((current) => ({ ...current, [key]: !current[key] }))
  }

  return (
    <table className="metric-table gpu-device-table">
      <thead>
        <tr>
          <th aria-label={t('labels.details')} />
          {columns.map((column) => (
            <th key={column.key}>{column.label}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {devices.map((device) => {
          const key = deviceKey(device)
          const isExpanded = Boolean(expanded[key])
          return (
            <Fragment key={key}>
              <tr>
                <td>
                  <button
                    aria-expanded={isExpanded}
                    aria-label={t('labels.details')}
                    className="row-toggle"
                    onClick={() => toggle(key)}
                    type="button"
                  >
                    {isExpanded ? '−' : '+'}
                  </button>
                </td>
                {columns.map((column) => (
                  <td key={column.key}>{column.render(device)}</td>
                ))}
              </tr>
              {isExpanded ? (
                <tr className="gpu-detail-row">
                  <td colSpan={columns.length + 1}>
                    <DetailList items={details(device)} />
                  </td>
                </tr>
              ) : null}
            </Fragment>
          )
        })}
        {devices.length === 0 ? (
          <tr>
            <td colSpan={columns.length + 1}>{t('states.none')}</td>
          </tr>
        ) : null}
      </tbody>
    </table>
  )
}

function temperatureText(value) {
  return formatOptional(value, 'C')
}

function wattsText(value) {
  return formatOptional(value, 'W', hasNumber(value) && Math.abs(value) < 10 ? 2 : 1)
}

function mhzText(value) {
  return formatOptional(value, 'MHz', 0)
}

function memoryText(metrics, sharedLabel) {
  return formatMemoryRange(metrics?.memoryUsedMegabytes, metrics?.memoryTotalMegabytes, metrics?.memoryShared ? sharedLabel : '')
}

function memoryPercentText(metrics) {
  return hasNumber(metrics?.memoryTotalMegabytes) && metrics.memoryTotalMegabytes > 0
    ? formatPercent(metrics.memoryUtilizationPercent)
    : '—'
}

function NvidiaBody({ section }) {
  const { t } = useTranslation()
  const columns = [
    { key: 'model', label: t('labels.model'), render: (device) => device.model || device.uuid || '—' },
    { key: 'vram', label: t('labels.vram'), render: (device) => memoryText(device.metrics, t('labels.shared')) },
    { key: 'vramPercent', label: t('labels.vramUtilization'), render: (device) => memoryPercentText(device.metrics) },
    { key: 'utilization', label: t('labels.utilization'), render: (device) => formatPercent(device.metrics?.gpuUtilizationPercent) },
    { key: 'temperature', label: t('labels.temperature'), render: (device) => temperatureText(device.metrics?.temperatureCelsius) },
    { key: 'power', label: t('labels.power'), render: (device) => wattsText(device.metrics?.powerUsageWatts) },
    { key: 'smClock', label: t('labels.smClock'), render: (device) => mhzText(device.metrics?.smClockMHz) },
  ]

  const details = (device) => [
    { label: t('labels.uuid'), value: device.uuid },
    { label: t('labels.busId'), value: device.busId },
    { label: t('labels.driverVersion'), value: device.driverVersion },
    { label: t('labels.migProfile'), value: device.migProfile },
    { label: t('labels.xidErrors'), value: hasNumber(device.xidErrors) ? formatNumber(device.xidErrors) : null },
    { label: t('labels.eccUncorrectable'), value: hasNumber(device.uncorrectableEccErrors) ? formatNumber(device.uncorrectableEccErrors) : null },
    { label: t('labels.memoryClock'), value: hasNumber(device.metrics?.memoryClockMHz) ? mhzText(device.metrics.memoryClockMHz) : null },
    { label: t('labels.memoryTemperature'), value: hasNumber(device.memoryTemperatureCelsius) ? temperatureText(device.memoryTemperatureCelsius) : null },
    { label: t('labels.memoryActivity'), value: hasNumber(device.memoryCopyUtilizationPercent) ? formatPercent(device.memoryCopyUtilizationPercent) : null },
  ]

  return (
    <>
      <div className="pill-row">
        <span className="pill">{`${t('labels.endpoint')}: ${section.endpoint || '—'}`}</span>
      </div>
      <DeviceTable columns={columns} details={details} devices={section.devices || []} />
    </>
  )
}

function AmdBody({ section }) {
  const { t } = useTranslation()
  const columns = [
    {
      key: 'model',
      label: t('labels.model'),
      render: (device) => (device.partitionId != null ? `${device.model || '—'} (${t('labels.partition')} ${device.partitionId})` : device.model || '—'),
    },
    { key: 'vram', label: t('labels.vram'), render: (device) => memoryText(device.metrics, t('labels.shared')) },
    { key: 'vramPercent', label: t('labels.vramUtilization'), render: (device) => memoryPercentText(device.metrics) },
    { key: 'gtt', label: t('labels.gtt'), render: (device) => formatMemoryRange(device.gttUsedMegabytes, device.gttTotalMegabytes) },
    { key: 'utilization', label: t('labels.utilization'), render: (device) => formatPercent(device.metrics?.gpuUtilizationPercent) },
    { key: 'memoryActivity', label: t('labels.memoryActivity'), render: (device) => formatPercent(device.memoryActivityPercent) },
    {
      key: 'temperature',
      label: t('labels.edgeHotspot'),
      render: (device) => `${temperatureText(device.metrics?.temperatureCelsius)} / ${temperatureText(device.hotspotTemperatureCelsius)}`,
    },
    { key: 'power', label: t('labels.power'), render: (device) => wattsText(device.metrics?.powerUsageWatts) },
  ]

  const details = (device) => [
    { label: t('labels.uuid'), value: device.uuid },
    { label: t('labels.busId'), value: device.busId },
    { label: t('labels.driverVersion'), value: device.driverVersion },
    { label: t('labels.computePartition'), value: device.computePartition },
    { label: t('labels.memoryPartition'), value: device.memoryPartition },
    { label: t('labels.eccCorrectable'), value: hasNumber(device.correctableEccErrors) ? formatNumber(device.correctableEccErrors) : null },
    { label: t('labels.eccUncorrectable'), value: hasNumber(device.uncorrectableEccErrors) ? formatNumber(device.uncorrectableEccErrors) : null },
    { label: t('labels.smClock'), value: hasNumber(device.metrics?.smClockMHz) ? mhzText(device.metrics.smClockMHz) : null },
    { label: t('labels.memoryClock'), value: hasNumber(device.metrics?.memoryClockMHz) ? mhzText(device.metrics.memoryClockMHz) : null },
    { label: t('labels.memoryTemperature'), value: hasNumber(device.memoryTemperatureCelsius) ? temperatureText(device.memoryTemperatureCelsius) : null },
    { label: t('labels.fieldSources'), value: fieldSourcesText(device.fieldSources, t) },
  ]

  return (
    <>
      <div className="pill-row">
        {section.gapFillSource ? (
          <span className="pill">{`${t('labels.gapFill')}: ${t(`sources.${section.gapFillSource}`, { defaultValue: section.gapFillSource })}`}</span>
        ) : null}
        {section.libraryVersion ? <span className="pill">{`${t('labels.libraryVersion')}: ${section.libraryVersion}`}</span> : null}
        <span className="pill">{`${t('labels.endpoint')}: ${section.endpoint || '—'}`}</span>
      </div>
      <DeviceTable columns={columns} details={details} devices={section.devices || []} />
    </>
  )
}

function AppleBody({ section }) {
  const { t } = useTranslation()
  const devices = section.devices || []
  const usedMegabytes = sum(devices.filter((device) => device.unifiedMemory), (device) => device.metrics?.memoryUsedMegabytes)
  const columns = [
    { key: 'model', label: t('labels.model'), render: (device) => device.model || '—' },
    { key: 'utilization', label: t('labels.utilization'), render: (device) => formatPercent(device.metrics?.gpuUtilizationPercent) },
    { key: 'renderer', label: t('labels.rendererUtilization'), render: (device) => formatPercent(device.rendererUtilizationPercent) },
    { key: 'tiler', label: t('labels.tilerUtilization'), render: (device) => formatPercent(device.tilerUtilizationPercent) },
    { key: 'memory', label: t('labels.gpuMemoryInUse'), render: (device) => memoryText(device.metrics, t('labels.shared')) },
    { key: 'power', label: t('labels.power'), render: (device) => wattsText(device.metrics?.powerUsageWatts) },
    { key: 'frequency', label: t('labels.frequency'), render: (device) => mhzText(device.metrics?.smClockMHz) },
    { key: 'temperature', label: t('labels.temperature'), render: (device) => temperatureText(device.metrics?.temperatureCelsius) },
  ]

  const details = (device) => [
    { label: t('labels.gpuCores'), value: hasNumber(device.gpuCoreCount) ? formatNumber(device.gpuCoreCount) : null },
    { label: t('labels.allocatedMemory'), value: hasNumber(device.allocatedSystemMemoryMegabytes) ? formatMegabytes(device.allocatedSystemMemoryMegabytes) : null },
    { label: t('labels.activeResidency'), value: hasNumber(device.activeResidencyPercent) ? formatPercent(device.activeResidencyPercent) : null },
    { label: t('labels.maxTemperature'), value: hasNumber(device.maxTemperatureCelsius) ? temperatureText(device.maxTemperatureCelsius) : null },
    {
      label: t('labels.temperatureSource'),
      value: section.temperatureSource ? t(`temperatureSources.${section.temperatureSource}`, { defaultValue: section.temperatureSource }) : null,
    },
    { label: t('labels.wiredLimit'), value: hasNumber(section.wiredLimitMegabytes) ? formatMegabytes(section.wiredLimitMegabytes) : null },
    { label: t('labels.ioClass'), value: device.busId },
    { label: t('labels.driverVersion'), value: device.driverVersion },
    {
      label: t('labels.nativeFeatures'),
      value: (section.features || []).map((feature) => t(`appleFeatures.${feature}`, { defaultValue: feature })).join(', ') || null,
    },
  ]

  return (
    <>
      <div className="pill-row">
        {section.chip ? <span className="pill">{section.chip}</span> : null}
        {hasNumber(section.gpuCoreCount) ? <span className="pill">{t('labels.coreGpu', { count: section.gpuCoreCount })}</span> : null}
        {section.metalFamily ? <span className="pill">{`${t('labels.metalFamily')}: ${section.metalFamily}`}</span> : null}
        {section.thermalState ? (
          <span className={`pill ${thermalStateTone(section.thermalState)}`.trim()}>
            {`${t('labels.thermalState')}: ${t(`thermalState.${section.thermalState}`)}`}
          </span>
        ) : null}
      </div>
      <UnifiedMemoryBar
        limitLabel={t('labels.recommendedWorkingSet')}
        limitMegabytes={section.recommendedMaxWorkingSetMegabytes}
        totalMegabytes={section.hostMemoryTotalMegabytes}
        usedMegabytes={usedMegabytes}
      />
      <DeviceTable columns={columns} details={details} devices={devices} />
    </>
  )
}

function QualcommBody({ section, hostMemoryTotalMegabytes }) {
  const { t } = useTranslation()
  const devices = section.devices || []
  const usedMegabytes = sum(devices, (device) => device.metrics?.memoryUsedMegabytes)
  const budgetMegabytes = sum(devices, (device) => device.sharedMemoryBudgetMegabytes)

  function utilizationText(device) {
    const value = formatPercent(device.metrics?.gpuUtilizationPercent)
    if (!device.fieldSources?.utilization) {
      return '—'
    }

    if (device.utilizationScope && device.utilizationScope !== 'device') {
      return `${value} (${t(`utilizationScope.${device.utilizationScope}`)})`
    }

    return value
  }

  const columns = [
    { key: 'model', label: t('labels.model'), render: (device) => device.model || '—' },
    {
      key: 'memory',
      label: t('labels.sharedMemory'),
      render: (device) => formatMemoryRange(device.sharedMemoryUsedMegabytes ?? device.metrics?.memoryUsedMegabytes, device.sharedMemoryBudgetMegabytes ?? device.metrics?.memoryTotalMegabytes),
    },
    { key: 'utilization', label: t('labels.utilization'), render: utilizationText },
    { key: 'engine3d', label: t('labels.engine3d'), render: (device) => formatPercent(device.engineUtilization?.['3d']) },
    { key: 'engineCompute', label: t('labels.engineCompute'), render: (device) => formatPercent(device.engineUtilization?.compute) },
    {
      key: 'frequency',
      label: t('labels.frequency'),
      render: (device) => `${mhzText(device.currentFrequencyMHz)} / ${mhzText(device.maxFrequencyMHz)}`,
    },
    { key: 'temperature', label: t('labels.temperature'), render: (device) => temperatureText(device.metrics?.temperatureCelsius) },
  ]

  const details = (device) => [
    {
      label: t('labels.engines'),
      value: Object.entries(device.engineUtilization || {})
        .map(([engine, value]) => `${t(`engines.${engine}`, { defaultValue: engine })}: ${formatPercent(value)}`)
        .join(', ') || null,
    },
    { label: t('labels.dedicatedMemory'), value: device.dedicatedMemoryBudgetMegabytes ? formatMemoryRange(device.dedicatedMemoryUsedMegabytes, device.dedicatedMemoryBudgetMegabytes) : null },
    { label: t('labels.adapterLuid'), value: device.adapterLuid },
    { label: t('labels.compatible'), value: device.compatible },
    { label: t('labels.driverVersion'), value: device.driverVersion },
    { label: t('labels.maxTemperature'), value: hasNumber(device.maxTemperatureCelsius) ? temperatureText(device.maxTemperatureCelsius) : null },
    { label: t('labels.fieldSources'), value: fieldSourcesText(device.fieldSources, t) },
  ]

  return (
    <>
      <UnifiedMemoryBar
        limitLabel={t('labels.memoryBudget')}
        limitMegabytes={budgetMegabytes}
        totalMegabytes={hostMemoryTotalMegabytes}
        usedMegabytes={usedMegabytes}
      />
      <DeviceTable columns={columns} details={details} devices={devices} />
    </>
  )
}

function VendorBody({ vendor, section, hostMemoryTotalMegabytes }) {
  if (vendor === 'nvidia') return <NvidiaBody section={section} />
  if (vendor === 'amd') return <AmdBody section={section} />
  if (vendor === 'apple') return <AppleBody section={section} />
  return <QualcommBody hostMemoryTotalMegabytes={hostMemoryTotalMegabytes} section={section} />
}

export function GpuVendorSections({ data, hideUnsupported = false }) {
  const { t } = useTranslation()
  const collection = data?.collection || {}
  const hostMemoryTotalMegabytes = hasNumber(data?.memory?.totalBytes) ? data.memory.totalBytes / 1024 / 1024 : null

  return (
    <>
      {gpuVendors.map((vendor) => {
        const status = collection[vendor.key] || null
        const section = data?.[vendor.key] || null
        if (hideUnsupported && !section && status && status.supported === false) {
          return null
        }

        return (
          <SectionCard
            className="overview-span-2 gpu-vendor-section"
            description={t(`descriptions.${vendor.key}`)}
            extra={section ? <SourcePill source={section.source} /> : null}
            key={vendor.key}
            status={status}
            title={t(`sections.${vendor.key}`)}
          >
            {section ? (
              <VendorBody hostMemoryTotalMegabytes={hostMemoryTotalMegabytes} section={section} vendor={vendor.key} />
            ) : (
              <div className="section-placeholder">
                <p>{status?.message || t(`messages.${vendor.key}Missing`)}</p>
                <a className="guide-link" href={`${guideBaseUrl}${vendor.guide}`} rel="noreferrer" target="_blank">
                  {t('messages.setupGuide')}
                </a>
              </div>
            )}
          </SectionCard>
        )
      })}
    </>
  )
}
