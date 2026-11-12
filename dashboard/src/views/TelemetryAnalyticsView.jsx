import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { JsonModal } from '../components/JsonModal'
import { RefreshButton } from '../components/RefreshButton'
import { StatCard } from '../components/StatCard'
import {
  formatBytes,
  formatDateTime,
  formatDateTimeWithSeconds,
  formatNumber,
  formatPercent,
  thermalStateOrder,
} from '../i18n/formatters'
import i18n from '../i18n'
import {
  getTelemetryHistoryStatus,
  getTelemetrySample,
  rollupTelemetryHistory,
  searchTelemetryHistory,
} from '../utils/api'

export const metricDescriptors = [
  { key: 'cpu', field: 'averageCpuUtilizationPercent', recordField: 'cpuUtilizationPercent', labelKey: 'analytics.metrics.cpu', type: 'percent' },
  { key: 'memory', field: 'averageMemoryUtilizationPercent', recordField: 'memoryUtilizationPercent', labelKey: 'analytics.metrics.memory', type: 'percent' },
  { key: 'gpu', field: 'averageGpuUtilizationPercent', vendorField: 'averageUtilizationPercent', recordField: 'gpuAverageUtilizationPercent', labelKey: 'analytics.metrics.gpu', type: 'percent' },
  { key: 'gpuMemory', field: 'averageGpuMemoryUtilizationPercent', vendorField: 'averageMemoryUtilizationPercent', recordField: 'gpuAverageMemoryUtilizationPercent', labelKey: 'analytics.metrics.gpuMemory', type: 'percent' },
  { key: 'gpuMemoryUsed', field: 'averageGpuMemoryUsedMegabytes', vendorField: 'averageMemoryUsedMegabytes', labelKey: 'analytics.metrics.gpuMemoryUsed', type: 'megabytes' },
  { key: 'gpuTemperature', field: 'averageGpuTemperatureCelsius', vendorField: 'averageTemperatureCelsius', recordField: 'gpuAverageTemperatureCelsius', labelKey: 'analytics.metrics.gpuTemperature', type: 'temperature' },
  { key: 'gpuPower', field: 'averageGpuPowerUsageWatts', vendorField: 'averagePowerUsageWatts', recordField: 'gpuTotalPowerUsageWatts', labelKey: 'analytics.metrics.gpuPower', type: 'watts' },
  { key: 'amdHotspot', vendorField: 'averageHotspotTemperatureCelsius', vendors: ['amd'], labelKey: 'analytics.metrics.amdHotspot', type: 'temperature' },
  { key: 'amdGtt', vendorField: 'averageGttUsedMegabytes', vendors: ['amd'], labelKey: 'analytics.metrics.amdGtt', type: 'megabytes' },
  { key: 'appleWorkingSet', vendorField: 'averageWorkingSetMegabytes', vendors: ['apple'], labelKey: 'analytics.metrics.appleWorkingSet', type: 'megabytes' },
  { key: 'appleThermalState', vendorField: 'maxThermalState', vendors: ['apple'], labelKey: 'analytics.metrics.appleThermalState', type: 'thermalState' },
  { key: 'qualcommMemoryBudget', vendorField: 'averageMemoryBudgetMegabytes', vendors: ['qualcomm'], labelKey: 'analytics.metrics.qualcommMemoryBudget', type: 'megabytes' },
  { key: 'networkRx', field: 'averageNetworkReceiveBytesPerSecond', recordField: 'networkReceiveBytesPerSecond', labelKey: 'analytics.metrics.networkRx', type: 'bytesPerSecond' },
  { key: 'networkTx', field: 'averageNetworkTransmitBytesPerSecond', recordField: 'networkTransmitBytesPerSecond', labelKey: 'analytics.metrics.networkTx', type: 'bytesPerSecond' },
  { key: 'diskRead', field: 'averageDiskReadOperationsPerSecond', recordField: 'diskReadOperationsPerSecond', labelKey: 'analytics.metrics.diskRead', type: 'number' },
  { key: 'diskWrite', field: 'averageDiskWriteOperationsPerSecond', recordField: 'diskWriteOperationsPerSecond', labelKey: 'analytics.metrics.diskWrite', type: 'number' },
  { key: 'ollamaLoaded', field: 'averageOllamaLoadedModelCount', recordField: 'ollamaLoadedModelCount', labelKey: 'analytics.metrics.ollamaLoaded', type: 'number' },
  { key: 'vllmRunning', field: 'averageVllmRunningRequests', recordField: 'vllmRunningRequests', labelKey: 'analytics.metrics.vllmRunning', type: 'number' },
  { key: 'vllmWaiting', field: 'averageVllmWaitingRequests', recordField: 'vllmWaitingRequests', labelKey: 'analytics.metrics.vllmWaiting', type: 'number' },
  { key: 'vllmGpuCache', field: 'averageVllmGpuCacheUsagePercent', recordField: 'vllmGpuCacheUsagePercent', labelKey: 'analytics.metrics.vllmGpuCache', type: 'percent' },
  { key: 'utilyzeDevices', field: 'averageUtilyzeDeviceCount', recordField: 'utilyzeDeviceCount', labelKey: 'analytics.metrics.utilyzeDevices', type: 'number' },
]

export const gpuVendorKeys = ['nvidia', 'amd', 'apple', 'qualcomm']
const bucketOptions = [1, 15, 60, 360, 1440]
const pageSizeOptions = [10, 25, 50, 100]
const refreshIntervals = [
  { value: 5000, label: '5s' },
  { value: 10000, label: '10s' },
  { value: 30000, label: '30s' },
  { value: 60000, label: '60s' },
  { value: 300000, label: '5m' },
]
const quickRangeSettings = {
  hour: { bucketMinutes: 1, sampleCount: 60 },
  day: { bucketMinutes: 15, sampleCount: 96 },
  week: { bucketMinutes: 60, sampleCount: 144 },
  month: { bucketMinutes: 360, sampleCount: 112 },
}

function quickRangeMilliseconds(range) {
  return range.bucketMinutes * range.sampleCount * 60 * 1000
}

function dateInputValue(date) {
  const adjusted = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return adjusted.toISOString().slice(0, 16)
}

function parseDateInput(value) {
  if (!value) {
    return null
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return null
  }

  return parsed.toISOString()
}

function initialStartInput() {
  return dateInputValue(new Date(Date.now() - quickRangeMilliseconds(quickRangeSettings.hour)))
}

function initialEndInput() {
  return dateInputValue(new Date())
}

function emptyFilters() {
  return {
    hostname: '',
    hostPlatform: '',
    gpuUuid: '',
    gpuModel: '',
    gpuVendor: '',
    nvidiaAvailable: '',
    amdAvailable: '',
    appleGpuAvailable: '',
    qualcommAvailable: '',
    ollamaAvailable: '',
    vllmAvailable: '',
    utilyzeAvailable: '',
    minCpuUtilizationPercent: '',
    maxCpuUtilizationPercent: '',
    minMemoryUtilizationPercent: '',
    maxMemoryUtilizationPercent: '',
    minGpuUtilizationPercent: '',
    maxGpuUtilizationPercent: '',
    minGpuTemperatureCelsius: '',
    maxGpuTemperatureCelsius: '',
  }
}

function parseBoolean(value) {
  if (value === 'true') {
    return true
  }

  if (value === 'false') {
    return false
  }

  return null
}

function parseNumberOrNull(value) {
  if (value === '' || value == null) {
    return null
  }

  const parsed = Number(value)
  if (Number.isNaN(parsed)) {
    return null
  }

  return parsed
}

function cleanString(value) {
  return value && value.trim() ? value.trim() : null
}

function buildSearchFilter(filters, startInput, endInput, page, pageSize) {
  return {
    hostname: cleanString(filters.hostname),
    hostPlatform: cleanString(filters.hostPlatform),
    gpuUuid: cleanString(filters.gpuUuid),
    gpuModel: cleanString(filters.gpuModel),
    gpuVendor: cleanString(filters.gpuVendor),
    nvidiaAvailable: parseBoolean(filters.nvidiaAvailable),
    amdAvailable: parseBoolean(filters.amdAvailable),
    appleGpuAvailable: parseBoolean(filters.appleGpuAvailable),
    qualcommAvailable: parseBoolean(filters.qualcommAvailable),
    ollamaAvailable: parseBoolean(filters.ollamaAvailable),
    vllmAvailable: parseBoolean(filters.vllmAvailable),
    utilyzeAvailable: parseBoolean(filters.utilyzeAvailable),
    startUtc: parseDateInput(startInput),
    endUtc: parseDateInput(endInput),
    minCpuUtilizationPercent: parseNumberOrNull(filters.minCpuUtilizationPercent),
    maxCpuUtilizationPercent: parseNumberOrNull(filters.maxCpuUtilizationPercent),
    minMemoryUtilizationPercent: parseNumberOrNull(filters.minMemoryUtilizationPercent),
    maxMemoryUtilizationPercent: parseNumberOrNull(filters.maxMemoryUtilizationPercent),
    minGpuUtilizationPercent: parseNumberOrNull(filters.minGpuUtilizationPercent),
    maxGpuUtilizationPercent: parseNumberOrNull(filters.maxGpuUtilizationPercent),
    minGpuTemperatureCelsius: parseNumberOrNull(filters.minGpuTemperatureCelsius),
    maxGpuTemperatureCelsius: parseNumberOrNull(filters.maxGpuTemperatureCelsius),
    page,
    pageSize,
  }
}

function buildRollupRequest(filters, startInput, endInput, bucketMinutes) {
  return {
    hostname: cleanString(filters.hostname),
    gpuUuid: cleanString(filters.gpuUuid),
    gpuVendor: cleanString(filters.gpuVendor),
    startUtc: parseDateInput(startInput),
    endUtc: parseDateInput(endInput),
    bucketMinutes,
    includeGpuVendorBreakdown: true,
    includeEmptyBuckets: true,
  }
}

function formatMetric(value, type) {
  if (type === 'percent') {
    return formatPercent(value)
  }

  if (type === 'temperature') {
    return value == null ? formatNumber(value) : `${formatNumber(value, { maximumFractionDigits: 1 })} C`
  }

  if (type === 'watts') {
    return value == null ? formatNumber(value) : `${formatNumber(value, { maximumFractionDigits: 1 })} W`
  }

  if (type === 'bytesPerSecond') {
    return value == null ? formatBytes(value) : `${formatBytes(value)}/s`
  }

  if (type === 'megabytes') {
    return value == null ? formatBytes(value) : formatBytes(value * 1024 * 1024)
  }

  if (type === 'thermalState') {
    if (value == null) {
      return formatNumber(value)
    }

    return i18n.t(`thermalState.${thermalStateOrder[Math.max(0, Math.min(3, Math.round(value)))]}`)
  }

  return formatNumber(value, { maximumFractionDigits: 2 })
}

export function vendorValue(bucket, vendor, field) {
  const value = bucket?.gpuVendors?.[vendor]?.[field]
  return value == null || Number.isNaN(value) ? null : value
}

export function vendorsWithData(buckets, field) {
  return gpuVendorKeys.filter((vendor) => buckets.some((bucket) => vendorValue(bucket, vendor, field) != null))
}

export function chartSeries(descriptor, buckets) {
  if (!descriptor.vendorField) {
    return [{ key: 'all', value: (bucket) => bucket[descriptor.field] }]
  }

  const allowed = descriptor.vendors || gpuVendorKeys
  const vendorSeries = vendorsWithData(buckets, descriptor.vendorField)
    .filter((vendor) => allowed.includes(vendor))
    .map((vendor) => ({ key: vendor, value: (bucket) => vendorValue(bucket, vendor, descriptor.vendorField) }))

  if (!descriptor.field) {
    return vendorSeries
  }

  const all = { key: 'all', value: (bucket) => bucket[descriptor.field] }
  if (vendorSeries.length > 1) {
    return [all, ...vendorSeries]
  }

  return vendorSeries.length === 1 ? vendorSeries : [all]
}

export function descriptorAvailable(descriptor, buckets) {
  if (!descriptor.vendors) {
    return true
  }

  return vendorsWithData(buckets, descriptor.vendorField).some((vendor) => descriptor.vendors.includes(vendor))
}

export function weightedVendorAverage(buckets, vendor, field) {
  let total = 0
  let count = 0

  buckets.forEach((bucket) => {
    const entry = bucket?.gpuVendors?.[vendor]
    const value = entry?.[field]
    const sampleCount = entry?.sampleCount || 0
    if (value != null && !Number.isNaN(value) && sampleCount > 0) {
      total += value * sampleCount
      count += sampleCount
    }
  })

  return count > 0 ? total / count : null
}

export function weightedAverage(buckets, field) {
  let total = 0
  let count = 0

  buckets.forEach((bucket) => {
    const value = bucket[field]
    const sampleCount = bucket.sampleCount || 0
    if (value != null && !Number.isNaN(value) && sampleCount > 0) {
      total += value * sampleCount
      count += sampleCount
    }
  })

  return count > 0 ? total / count : null
}

function hasValue(value) {
  return value != null && !Number.isNaN(value)
}

function formatMegabytes(value) {
  return hasValue(value) ? formatBytes(value * 1024 * 1024) : formatBytes(null)
}

function formatOpsPerSecond(value) {
  return hasValue(value) ? `${formatNumber(value, { maximumFractionDigits: 2 })}/s` : formatNumber(null)
}

function formatRange(minValue, maxValue, type) {
  if (!hasValue(minValue) && !hasValue(maxValue)) {
    return formatNumber(null)
  }

  return `${formatMetric(minValue, type)} - ${formatMetric(maxValue, type)}`
}

function buildTooltipRows(point, descriptor, t) {
  const bucket = point.bucket
  const rows = []

  if (descriptor.key === 'cpu') {
    if (hasValue(bucket.averageCpuUtilizationPercent) && hasValue(bucket.averageLogicalCoreCount)) {
      const usedCores = (bucket.averageCpuUtilizationPercent / 100) * bucket.averageLogicalCoreCount
      rows.push(`${t('analytics.tooltip.cpuCores')}: ${formatNumber(usedCores, { maximumFractionDigits: 1 })} / ${formatNumber(bucket.averageLogicalCoreCount, { maximumFractionDigits: 1 })}`)
    }
  } else if (descriptor.key === 'memory') {
    if (hasValue(bucket.averageMemoryUsedBytes) || hasValue(bucket.averageMemoryTotalBytes)) {
      rows.push(`${t('analytics.tooltip.memoryUsed')}: ${formatBytes(bucket.averageMemoryUsedBytes)} / ${formatBytes(bucket.averageMemoryTotalBytes)}`)
    }

    if (hasValue(bucket.averageMemoryAvailableBytes)) {
      rows.push(`${t('analytics.tooltip.memoryAvailable')}: ${formatBytes(bucket.averageMemoryAvailableBytes)}`)
    }
  } else if (descriptor.key === 'gpu') {
    rows.push(`${t('analytics.tooltip.range')}: ${formatRange(bucket.minGpuUtilizationPercent, bucket.maxGpuUtilizationPercent, 'percent')}`)
    if (hasValue(bucket.averageGpuDeviceCount)) {
      rows.push(`${t('analytics.tooltip.gpuDevices')}: ${formatNumber(bucket.averageGpuDeviceCount, { maximumFractionDigits: 1 })}`)
    }
  } else if (descriptor.key === 'gpuMemory') {
    if (hasValue(bucket.averageGpuMemoryUsedMegabytes) || hasValue(bucket.averageGpuMemoryTotalMegabytes)) {
      rows.push(`${t('analytics.tooltip.gpuMemoryUsed')}: ${formatMegabytes(bucket.averageGpuMemoryUsedMegabytes)} / ${formatMegabytes(bucket.averageGpuMemoryTotalMegabytes)}`)
    }

    if (hasValue(bucket.averageGpuDeviceCount)) {
      rows.push(`${t('analytics.tooltip.gpuDevices')}: ${formatNumber(bucket.averageGpuDeviceCount, { maximumFractionDigits: 1 })}`)
    }
  } else if (descriptor.key === 'gpuTemperature') {
    rows.push(`${t('analytics.tooltip.range')}: ${formatRange(bucket.minGpuTemperatureCelsius, bucket.maxGpuTemperatureCelsius, 'temperature')}`)
    if (hasValue(bucket.averageGpuDeviceCount)) {
      rows.push(`${t('analytics.tooltip.gpuDevices')}: ${formatNumber(bucket.averageGpuDeviceCount, { maximumFractionDigits: 1 })}`)
    }
  } else if (descriptor.key === 'gpuPower') {
    if (hasValue(bucket.averageGpuDeviceCount)) {
      rows.push(`${t('analytics.tooltip.gpuDevices')}: ${formatNumber(bucket.averageGpuDeviceCount, { maximumFractionDigits: 1 })}`)
    }
  } else if (descriptor.key === 'networkRx' || descriptor.key === 'networkTx') {
    const receive = bucket.averageNetworkReceiveBytesPerSecond
    const transmit = bucket.averageNetworkTransmitBytesPerSecond
    rows.push(`${t('analytics.tooltip.networkReceive')}: ${formatMetric(receive, 'bytesPerSecond')}`)
    rows.push(`${t('analytics.tooltip.networkTransmit')}: ${formatMetric(transmit, 'bytesPerSecond')}`)

    if (hasValue(receive) || hasValue(transmit)) {
      rows.push(`${t('analytics.tooltip.networkTotal')}: ${formatMetric((receive || 0) + (transmit || 0), 'bytesPerSecond')}`)
    }
  } else if (descriptor.key === 'diskRead' || descriptor.key === 'diskWrite') {
    rows.push(`${t('analytics.tooltip.diskReads')}: ${formatOpsPerSecond(bucket.averageDiskReadOperationsPerSecond)}`)
    rows.push(`${t('analytics.tooltip.diskWrites')}: ${formatOpsPerSecond(bucket.averageDiskWriteOperationsPerSecond)}`)

    if (hasValue(bucket.averageDiskReadQueueDepth) || hasValue(bucket.averageDiskWriteQueueDepth)) {
      rows.push(`${t('analytics.tooltip.diskQueue')}: ${formatNumber(bucket.averageDiskReadQueueDepth, { maximumFractionDigits: 2 })} / ${formatNumber(bucket.averageDiskWriteQueueDepth, { maximumFractionDigits: 2 })}`)
    }
  } else if (descriptor.key === 'ollamaLoaded') {
    if (hasValue(bucket.averageOllamaLoadedModelCount) || hasValue(bucket.averageOllamaAvailableModelCount)) {
      rows.push(`${t('analytics.tooltip.ollamaModels')}: ${formatNumber(bucket.averageOllamaLoadedModelCount, { maximumFractionDigits: 1 })} / ${formatNumber(bucket.averageOllamaAvailableModelCount, { maximumFractionDigits: 1 })}`)
    }
  } else if (descriptor.key === 'vllmRunning' || descriptor.key === 'vllmWaiting') {
    const running = bucket.averageVllmRunningRequests
    const waiting = bucket.averageVllmWaitingRequests
    rows.push(`${t('analytics.tooltip.vllmRunning')}: ${formatNumber(running, { maximumFractionDigits: 1 })}`)
    rows.push(`${t('analytics.tooltip.vllmWaiting')}: ${formatNumber(waiting, { maximumFractionDigits: 1 })}`)

    if (hasValue(running) || hasValue(waiting)) {
      rows.push(`${t('analytics.tooltip.vllmTotal')}: ${formatNumber((running || 0) + (waiting || 0), { maximumFractionDigits: 1 })}`)
    }
  }

  return rows
}

function StateBox({ title, body }) {
  return (
    <section className="surface state-box">
      <h2>{title}</h2>
      <p>{body}</p>
    </section>
  )
}

function seriesLabel(series, t) {
  return series.key === 'all' ? t('labels.allGpus') : t(`vendors.${series.key}`)
}

function buildSegments(points) {
  const segments = []
  let current = []
  points.forEach((point) => {
    if (point.value == null) {
      if (current.length > 0) segments.push(current)
      current = []
      return
    }

    current.push(point)
  })

  if (current.length > 0) segments.push(current)
  return segments
}

export function RollupChart({ buckets, descriptor, emptyLabel, label, metricLabel, sampleLabel, timeLabel }) {
  const { t } = useTranslation()
  const [hoveredIndex, setHoveredIndex] = useState(null)
  const [hiddenSeries, setHiddenSeries] = useState({})
  const width = 920
  const height = 292.5
  const padLeft = 64
  const padRight = 18
  const padTop = 18
  const padBottom = 40
  const plotWidth = width - padLeft - padRight
  const plotHeight = height - padTop - padBottom
  const series = chartSeries(descriptor, buckets)
  const multiSeries = Boolean(descriptor.vendorField)
  const visibleSeries = series.filter((item) => !hiddenSeries[item.key])
  const values = []
  visibleSeries.forEach((item) => buckets.forEach((bucket) => {
    const value = item.value(bucket)
    if (value != null) values.push(value)
  }))
  const hasData = buckets.length > 0 && values.length > 0
  const maxValue = descriptor.type === 'thermalState' ? 3 : (values.length > 0 ? Math.max(...values, 1) : 1)
  const yTickCount = descriptor.type === 'thermalState' ? 4 : (hasData ? 5 : 2)
  const yTicks = Array.from({ length: yTickCount }, (_, index) => {
    const ratio = yTickCount === 1 ? 0 : index / (yTickCount - 1)
    return {
      value: maxValue * (1 - ratio),
      y: padTop + ratio * plotHeight,
    }
  })
  const divisor = Math.max(1, buckets.length - 1)
  const xFor = (index) => padLeft + (index / divisor) * plotWidth
  const yFor = (value) => padTop + plotHeight - (value / maxValue) * plotHeight
  const plotted = visibleSeries.map((item) => {
    const points = buckets.map((bucket, index) => {
      const raw = item.value(bucket)
      const value = raw == null ? (multiSeries ? null : 0) : raw
      return { x: xFor(index), y: value == null ? null : yFor(value), value, bucket }
    })
    return { ...item, points, segments: buildSegments(points) }
  })
  const hoverZones = buckets.map((bucket, index) => {
    const x = xFor(index)
    const previousX = index === 0 ? padLeft : (xFor(index - 1) + x) / 2
    const nextX = index === buckets.length - 1 ? padLeft + plotWidth : (x + xFor(index + 1)) / 2
    return { index, x: previousX, width: nextX - previousX }
  })
  const firstBucket = buckets[0]
  const lastBucket = buckets[buckets.length - 1]
  const hoveredBucket = hoveredIndex == null ? null : buckets[hoveredIndex]
  const hoveredX = hoveredIndex == null ? 0 : xFor(hoveredIndex)
  const primaryPoint = hoveredIndex == null ? null : plotted.map((item) => item.points[hoveredIndex]).find((point) => point && point.value != null) || null
  const seriesRows = hoveredBucket && multiSeries
    ? plotted.map((item) => `${seriesLabel(item, t)}: ${formatMetric(item.points[hoveredIndex].value, descriptor.type)}`)
    : []
  const tooltipRows = hoveredBucket && primaryPoint ? [...seriesRows, ...buildTooltipRows(primaryPoint, descriptor, t)] : seriesRows
  const tooltipWidth = 300
  const tooltipHeight = 68 + tooltipRows.length * 15 - (multiSeries ? 15 : 0)
  const anchorY = primaryPoint?.y ?? padTop + plotHeight / 2
  const tooltipX = hoveredBucket
    ? (hoveredX + tooltipWidth + 14 > width - padRight ? hoveredX - tooltipWidth - 14 : hoveredX + 14)
    : 0
  const tooltipY = hoveredBucket
    ? Math.min(Math.max(padTop + 6, anchorY - 36), height - padBottom - tooltipHeight - 6)
    : 0

  function toggleSeries(key) {
    setHiddenSeries((current) => ({ ...current, [key]: !current[key] }))
  }

  return (
    <div className="chart-scroll">
      {multiSeries && series.length > 0 ? (
        <div className="chart-legend" role="group" aria-label={t('analytics.legendLabel')}>
          {series.map((item) => (
            <button
              aria-pressed={!hiddenSeries[item.key]}
              className={`chart-legend-item ${hiddenSeries[item.key] ? 'muted' : ''}`.trim()}
              key={item.key}
              onClick={() => toggleSeries(item.key)}
              type="button"
            >
              <i className={`chart-legend-swatch series-${item.key}`} />
              {seriesLabel(item, t)}
            </button>
          ))}
        </div>
      ) : null}
      <svg
        aria-label={label}
        className="rollup-chart"
        onMouseLeave={() => setHoveredIndex(null)}
        role="img"
        viewBox={`0 0 ${width} ${height}`}
      >
        <line className="chart-axis" x1={padLeft} x2={padLeft} y1={padTop} y2={padTop + plotHeight} />
        <line className="chart-axis" x1={padLeft} x2={padLeft + plotWidth} y1={padTop + plotHeight} y2={padTop + plotHeight} />
        {yTicks.map((tick) => (
          <g key={`${tick.y}-${tick.value}`}>
            <line className="chart-grid-line" x1={padLeft} x2={padLeft + plotWidth} y1={tick.y} y2={tick.y} />
            <text className="chart-axis-label chart-y-axis-label" x={padLeft - 8} y={tick.y + 3} textAnchor="end">
              {formatMetric(tick.value, descriptor.type)}
            </text>
          </g>
        ))}
        {hasData ? (
          <>
            {plotted.map((item) => (
              <g className={`chart-series series-${item.key}`} key={item.key}>
                {!multiSeries && item.segments.length > 0 ? (
                  <polygon
                    className="chart-area"
                    points={`${padLeft},${padTop + plotHeight} ${item.points.map((point) => `${point.x},${point.y}`).join(' ')} ${padLeft + plotWidth},${padTop + plotHeight}`}
                  />
                ) : null}
                {item.segments.map((segment) => (
                  <polyline className="chart-line" key={`${item.key}-${segment[0].x}`} points={segment.map((point) => `${point.x},${point.y}`).join(' ')} />
                ))}
                {item.points.filter((point) => point.value != null).map((point) => (
                  <circle className="chart-point" cx={point.x} cy={point.y} key={`${item.key}-${point.bucket.bucketStartUtc}-${point.x}`} r="1.4" />
                ))}
              </g>
            ))}
            {hoverZones.map((zone) => (
              <rect
                className="chart-hover-target"
                height={plotHeight}
                key={`${zone.index}-${zone.x}`}
                onMouseEnter={() => setHoveredIndex(zone.index)}
                onMouseMove={() => setHoveredIndex(zone.index)}
                width={zone.width}
                x={zone.x}
                y={padTop}
              />
            ))}
            <text className="chart-axis-label chart-x-axis-label" x={padLeft} y={height - 12} textAnchor="start">
              {formatDateTime(firstBucket.bucketStartUtc)}
            </text>
            <text className="chart-axis-label chart-x-axis-label" x={padLeft + plotWidth} y={height - 12} textAnchor="end">
              {formatDateTime(lastBucket.bucketEndUtc)}
            </text>
          </>
        ) : (
          <text className="chart-empty-label" x={padLeft + plotWidth / 2} y={padTop + plotHeight / 2} textAnchor="middle">
            {emptyLabel}
          </text>
        )}
        {hoveredBucket && hasData ? (
          <g className="chart-tooltip" pointerEvents="none">
            <line className="chart-tooltip-line" x1={hoveredX} x2={hoveredX} y1={padTop} y2={padTop + plotHeight} />
            {plotted.map((item) => {
              const point = item.points[hoveredIndex]
              return point && point.value != null ? (
                <circle className={`chart-active-point series-${item.key}`} cx={point.x} cy={point.y} key={item.key} r="2.25" />
              ) : null
            })}
            <rect className="chart-tooltip-box" height={tooltipHeight} rx="6" width={tooltipWidth} x={tooltipX} y={tooltipY} />
            <text className="chart-tooltip-text" x={tooltipX + 10} y={tooltipY + 16}>
              <tspan className="chart-tooltip-title">{metricLabel}</tspan>
              {!multiSeries ? <tspan x={tooltipX + 10} dy="15">{formatMetric(primaryPoint?.value, descriptor.type)}</tspan> : null}
              {tooltipRows.map((row, index) => (
                <tspan key={`${row}-${index}`} x={tooltipX + 10} dy="15">{row}</tspan>
              ))}
              <tspan x={tooltipX + 10} dy="15">{`${timeLabel}: ${formatDateTimeWithSeconds(hoveredBucket.bucketStartUtc)}`}</tspan>
              <tspan x={tooltipX + 10} dy="15">{`${sampleLabel}: ${formatNumber(hoveredBucket.sampleCount || 0)}`}</tspan>
            </text>
          </g>
        ) : null}
      </svg>
    </div>
  )
}

export function TelemetryAnalyticsView() {
  const { t } = useTranslation()
  const [startInput, setStartInput] = useState(initialStartInput)
  const [endInput, setEndInput] = useState(initialEndInput)
  const [bucketMinutes, setBucketMinutes] = useState(quickRangeSettings.hour.bucketMinutes)
  const [metricKey, setMetricKey] = useState('cpu')
  const [filters, setFilters] = useState(emptyFilters)
  const [appliedFilters, setAppliedFilters] = useState(emptyFilters)
  const [status, setStatus] = useState(null)
  const [searchResult, setSearchResult] = useState(null)
  const [rollupResult, setRollupResult] = useState(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [activeQuickRange, setActiveQuickRange] = useState('hour')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [modalValue, setModalValue] = useState('')
  const [modalTitle, setModalTitle] = useState('')
  const [modalCloseLabel, setModalCloseLabel] = useState('')
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [autoRefresh, setAutoRefresh] = useState(false)
  const [intervalMs, setIntervalMs] = useState(30000)
  const startInputRef = useRef(startInput)
  const endInputRef = useRef(endInput)

  useEffect(() => {
    startInputRef.current = startInput
  }, [startInput])

  useEffect(() => {
    endInputRef.current = endInput
  }, [endInput])

  const descriptor = useMemo(
    () => metricDescriptors.find((item) => item.key === metricKey) || metricDescriptors[0],
    [metricKey]
  )

  const buckets = rollupResult?.buckets || []
  const records = searchResult?.data || []
  const totalPages = searchResult?.totalPages || 0
  const latestRecord = records[0] || null
  const averageCpu = weightedAverage(buckets, 'averageCpuUtilizationPercent')
  const averageMemory = weightedAverage(buckets, 'averageMemoryUtilizationPercent')
  const averageGpu = weightedAverage(buckets, 'averageGpuUtilizationPercent')
  const averageGpuTemperature = weightedAverage(buckets, 'averageGpuTemperatureCelsius')

  useEffect(() => {
    let cancelled = false

    async function loadAnalytics() {
      try {
        setLoading(true)
        setError('')

        const searchFilter = buildSearchFilter(appliedFilters, startInput, endInput, page, pageSize)
        const rollupRequest = buildRollupRequest(appliedFilters, startInput, endInput, bucketMinutes)
        const [statusResult, searchResponse, rollupResponse] = await Promise.all([
          getTelemetryHistoryStatus(),
          searchTelemetryHistory(searchFilter),
          rollupTelemetryHistory(rollupRequest),
        ])

        if (!cancelled) {
          setStatus(statusResult)
          setSearchResult(searchResponse)
          setRollupResult(rollupResponse)
        }
      } catch (cause) {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : String(cause))
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    void loadAnalytics()

    return () => {
      cancelled = true
    }
  }, [appliedFilters, bucketMinutes, endInput, page, pageSize, refreshNonce, startInput])

  function slideWindowToNow() {
    const now = new Date()
    const start = new Date(startInputRef.current)
    const end = new Date(endInputRef.current)
    let durationMs = end.getTime() - start.getTime()
    if (!Number.isFinite(durationMs) || durationMs <= 0) {
      durationMs = quickRangeMilliseconds(quickRangeSettings.hour)
    }

    setStartInput(dateInputValue(new Date(now.getTime() - durationMs)))
    setEndInput(dateInputValue(now))
  }

  function handleRefresh() {
    slideWindowToNow()
    setRefreshNonce((value) => value + 1)
  }

  useEffect(() => {
    if (!autoRefresh) {
      return undefined
    }

    const timerId = window.setInterval(() => {
      handleRefresh()
    }, intervalMs)

    return () => window.clearInterval(timerId)
    // handleRefresh reads the latest range from refs, so it is safe to omit here.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoRefresh, intervalMs])

  function updateFilter(name, value) {
    setFilters((current) => ({
      ...current,
      [name]: value,
    }))
  }

  function applyFilters() {
    setPage(1)
    setAppliedFilters({ ...filters })
  }

  function resetFilters() {
    const nextFilters = emptyFilters()
    setFilters(nextFilters)
    setAppliedFilters(nextFilters)
    setPage(1)
  }

  function applyQuickRange(rangeKey) {
    const now = new Date()
    const range = quickRangeSettings[rangeKey] || quickRangeSettings.hour
    setStartInput(dateInputValue(new Date(now.getTime() - quickRangeMilliseconds(range))))
    setEndInput(dateInputValue(now))
    setBucketMinutes(range.bucketMinutes)
    setActiveQuickRange(rangeKey)
    setPage(1)
  }

  async function openSample(record) {
    const detail = await getTelemetrySample(record.id)
    setModalValue(JSON.stringify(detail, null, 2))
    setModalTitle(t('analytics.detailTitle'))
    setModalCloseLabel(t('analytics.actions.closeDetails'))
    setIsModalOpen(true)
  }

  function updateStartInput(value) {
    setStartInput(value)
    setActiveQuickRange('')
  }

  function updateEndInput(value) {
    setEndInput(value)
    setActiveQuickRange('')
  }

  function updateBucketMinutes(value) {
    setBucketMinutes(value)
    setActiveQuickRange('')
  }

  function openJsonModal() {
    const searchFilter = buildSearchFilter(appliedFilters, startInput, endInput, page, pageSize)
    const rollupRequest = buildRollupRequest(appliedFilters, startInput, endInput, bucketMinutes)
    const payload = {
      view: 'analytics',
      generatedUtc: new Date().toISOString(),
      controls: {
        activeQuickRange,
        startInput,
        endInput,
        bucketMinutes,
        metricKey,
        filters,
        appliedFilters,
        page,
        pageSize,
      },
      requests: {
        status: {
          method: 'GET',
          path: '/v1/telemetry/history/status',
        },
        search: {
          method: 'POST',
          path: '/v1/telemetry/history/search',
          body: searchFilter,
        },
        rollup: {
          method: 'POST',
          path: '/v1/telemetry/history/rollups',
          body: rollupRequest,
        },
      },
      responses: {
        status,
        search: searchResult,
        rollup: rollupResult,
      },
      derived: {
        selectedMetric: descriptor,
        bucketCount: buckets.length,
        visibleRecordCount: records.length,
        averageCpuUtilizationPercent: averageCpu,
        averageMemoryUtilizationPercent: averageMemory,
        averageGpuUtilizationPercent: averageGpu,
        averageGpuTemperatureCelsius: averageGpuTemperature,
      },
    }

    setModalValue(JSON.stringify(payload, null, 2))
    setModalTitle(t('analytics.jsonTitle'))
    setModalCloseLabel(t('analytics.actions.closeJson'))
    setIsModalOpen(true)
  }

  function statusTone() {
    if (!status) {
      return ''
    }

    return status.enabled ? 'success' : 'warning'
  }

  function statusLabel() {
    if (!status) {
      return t('analytics.status.unknown')
    }

    return status.enabled ? t('analytics.status.enabled') : t('analytics.status.disabled')
  }

  if (loading && !searchResult && !rollupResult) {
    return <StateBox title={t('analytics.loadingTitle')} body={t('analytics.loadingBody')} />
  }

  if (error && !searchResult && !rollupResult) {
    return <StateBox title={t('analytics.errorTitle')} body={`${t('analytics.errorBody')} ${error}`} />
  }

  return (
    <>
      <div className="view-grid analytics-view">
        <section className="surface toolbar analytics-toolbar">
          <div>
            <h2>{t('analytics.title')}</h2>
            <p>{t('analytics.subtitle')}</p>
          </div>
          <div className="toolbar-actions">
            <button className="button-secondary" onClick={openJsonModal} type="button">
              {t('analytics.actions.viewJson')}
            </button>
            <RefreshButton label={t('analytics.actions.refresh')} onClick={handleRefresh} />
            <label className="toggle">
              <span>{t('analytics.refreshInterval')}</span>
              <select
                className="select"
                disabled={!autoRefresh}
                value={intervalMs}
                onChange={(event) => setIntervalMs(Number(event.target.value))}
              >
                {refreshIntervals.map((interval) => (
                  <option key={interval.value} value={interval.value}>
                    {interval.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="toggle">
              <input
                checked={autoRefresh}
                onChange={(event) => setAutoRefresh(event.target.checked)}
                type="checkbox"
              />
              <span>{t('analytics.autoRefresh')}</span>
            </label>
            <span className={`pill ${statusTone()}`.trim()}>
              {statusLabel()}
            </span>
          </div>
        </section>

        {error ? (
          <div className="inline-alert warning" role="status">
            <strong>{t('analytics.refreshErrorTitle')}</strong>
            <span>{`${t('analytics.refreshErrorBody')} ${error}`}</span>
          </div>
        ) : null}

        {status && !status.enabled ? (
          <div className="inline-alert warning" role="status">
            <strong>{t('analytics.disabledTitle')}</strong>
            <span>{t('analytics.disabledBody')}</span>
          </div>
        ) : null}

        <section className="surface analytics-controls">
          <div className="quick-range-row" aria-label={t('analytics.quickRangesLabel')}>
            {['hour', 'day', 'week', 'month'].map((rangeKey) => (
              <button
                aria-pressed={activeQuickRange === rangeKey}
                className={activeQuickRange === rangeKey ? 'button-primary' : 'button-secondary'}
                key={rangeKey}
                onClick={() => applyQuickRange(rangeKey)}
                type="button"
              >
                {t(`analytics.quickRanges.${rangeKey}`)}
              </button>
            ))}
          </div>

          <div className="filter-grid">
            <label>
              <span>{t('analytics.filters.start')}</span>
              <input className="input" onChange={(event) => updateStartInput(event.target.value)} type="datetime-local" value={startInput} />
            </label>
            <label>
              <span>{t('analytics.filters.end')}</span>
              <input className="input" onChange={(event) => updateEndInput(event.target.value)} type="datetime-local" value={endInput} />
            </label>
            <label>
              <span>{t('analytics.filters.bucket')}</span>
              <select className="select" onChange={(event) => updateBucketMinutes(Number(event.target.value))} value={bucketMinutes}>
                {bucketOptions.map((value) => (
                  <option key={value} value={value}>
                    {t(`analytics.buckets.${value}`)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>{t('analytics.filters.metric')}</span>
              <select className="select" onChange={(event) => setMetricKey(event.target.value)} value={metricKey}>
                {metricDescriptors.filter((item) => item.key === metricKey || descriptorAvailable(item, buckets)).map((item) => (
                  <option key={item.key} value={item.key}>
                    {t(item.labelKey)}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </section>

        <div className="stats-grid">
          <StatCard label={t('analytics.cards.samples')} value={formatNumber(rollupResult?.totalSamples)} />
          <StatCard label={t('analytics.cards.cpu')} value={formatPercent(averageCpu)} />
          <StatCard label={t('analytics.cards.memory')} value={formatPercent(averageMemory)} />
          <StatCard label={t('analytics.cards.gpu')} value={formatPercent(averageGpu)} />
          {gpuVendorKeys.filter((vendor) => vendorsWithData(buckets, 'averageUtilizationPercent').includes(vendor)).map((vendor) => (
            <StatCard
              key={vendor}
              label={t('analytics.cards.vendorGpu', { vendor: t(`vendors.${vendor}`) })}
              value={formatPercent(weightedVendorAverage(buckets, vendor, 'averageUtilizationPercent'))}
            />
          ))}
          <StatCard label={t('analytics.cards.gpuTemperature')} value={formatMetric(averageGpuTemperature, 'temperature')} />
          <StatCard label={t('analytics.cards.latest')} value={latestRecord ? formatDateTimeWithSeconds(latestRecord.persistedUtc) : formatDateTime(null)} />
        </div>

        <section className="surface analytics-chart-panel">
          <div className="section-header">
            <div>
              <h2>{t('analytics.chartTitle')}</h2>
              <p>{t(descriptor.labelKey)}</p>
            </div>
            <span className="pill">{`${t('analytics.labels.bucketCount')}: ${formatNumber(buckets.length)}`}</span>
          </div>
          <RollupChart
            buckets={buckets}
            descriptor={descriptor}
            emptyLabel={t('analytics.emptyChart')}
            label={t('analytics.chartAria', { metric: t(descriptor.labelKey) })}
            metricLabel={t(descriptor.labelKey)}
            sampleLabel={t('analytics.cards.samples')}
            timeLabel={t('labels.collected')}
          />
        </section>

        <section className="surface analytics-controls">
          <div className="filter-grid">
            <label>
              <span>{t('analytics.filters.hostname')}</span>
              <input className="input" onChange={(event) => updateFilter('hostname', event.target.value)} value={filters.hostname} />
            </label>
            <label>
              <span>{t('analytics.filters.platform')}</span>
              <select className="select" onChange={(event) => updateFilter('hostPlatform', event.target.value)} value={filters.hostPlatform}>
                <option value="">{t('analytics.options.any')}</option>
                <option value="windows">{t('analytics.platforms.windows')}</option>
                <option value="linux">{t('analytics.platforms.linux')}</option>
                <option value="mac">{t('analytics.platforms.mac')}</option>
              </select>
            </label>
            <label>
              <span>{t('analytics.filters.gpuUuid')}</span>
              <input className="input" onChange={(event) => updateFilter('gpuUuid', event.target.value)} value={filters.gpuUuid} />
            </label>
            <label>
              <span>{t('analytics.filters.gpuModel')}</span>
              <input className="input" onChange={(event) => updateFilter('gpuModel', event.target.value)} value={filters.gpuModel} />
            </label>
            <label>
              <span>{t('analytics.filters.gpuVendor')}</span>
              <select className="select" data-testid="gpu-vendor-filter" onChange={(event) => updateFilter('gpuVendor', event.target.value)} value={filters.gpuVendor}>
                <option value="">{t('analytics.options.any')}</option>
                {gpuVendorKeys.map((vendor) => (
                  <option key={vendor} value={vendor}>{t(`vendors.${vendor}`)}</option>
                ))}
              </select>
            </label>
          </div>

          <div className="filter-grid compact">
            {['nvidiaAvailable', 'amdAvailable', 'appleGpuAvailable', 'qualcommAvailable', 'ollamaAvailable', 'vllmAvailable', 'utilyzeAvailable'].map((name) => (
              <label key={name}>
                <span>{t(`analytics.filters.${name}`)}</span>
                <select className="select" onChange={(event) => updateFilter(name, event.target.value)} value={filters[name]}>
                  <option value="">{t('analytics.options.any')}</option>
                  <option value="true">{t('analytics.options.yes')}</option>
                  <option value="false">{t('analytics.options.no')}</option>
                </select>
              </label>
            ))}
          </div>

          <div className="filter-grid compact">
            {[
              'minCpuUtilizationPercent',
              'maxCpuUtilizationPercent',
              'minMemoryUtilizationPercent',
              'maxMemoryUtilizationPercent',
              'minGpuUtilizationPercent',
              'maxGpuUtilizationPercent',
              'minGpuTemperatureCelsius',
              'maxGpuTemperatureCelsius',
            ].map((name) => (
              <label key={name}>
                <span>{t(`analytics.filters.${name}`)}</span>
                <input className="input" onChange={(event) => updateFilter(name, event.target.value)} type="number" value={filters[name]} />
              </label>
            ))}
          </div>

          <div className="toolbar-actions">
            <button className="button-primary" onClick={applyFilters} type="button">
              {t('analytics.actions.apply')}
            </button>
            <button className="button-secondary" onClick={resetFilters} type="button">
              {t('analytics.actions.reset')}
            </button>
          </div>
        </section>

        <section className="surface analytics-table-panel">
          <div className="section-header">
            <div>
              <h2>{t('analytics.tableTitle')}</h2>
              <p>{t('analytics.tableSubtitle')}</p>
            </div>
            <div className="toolbar-actions">
              <label className="toggle">
                <span>{t('analytics.labels.pageSize')}</span>
                <select className="select select-compact" onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1) }} value={pageSize}>
                  {pageSizeOptions.map((value) => (
                    <option key={value} value={value}>
                      {formatNumber(value)}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </div>

          <table className="metric-table history-table">
            <thead>
              <tr>
                <th>{t('analytics.table.collected')}</th>
                <th>{t('analytics.table.hostname')}</th>
                <th>{t('analytics.table.platform')}</th>
                <th>{t('analytics.table.cpu')}</th>
                <th>{t('analytics.table.memory')}</th>
                <th>{t('analytics.table.gpuVendor')}</th>
                <th>{t('analytics.table.gpu')}</th>
                <th>{t('analytics.table.gpuTemperature')}</th>
                <th>{t('analytics.table.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {records.map((record) => (
                <tr key={record.id}>
                  <td>{formatDateTimeWithSeconds(record.collectedUtc)}</td>
                  <td>{record.hostname || formatNumber(null)}</td>
                  <td>{record.hostPlatform || formatNumber(null)}</td>
                  <td>{formatPercent(record.cpuUtilizationPercent)}</td>
                  <td>{formatPercent(record.memoryUtilizationPercent)}</td>
                  <td>{record.gpuVendor ? t(`vendors.${record.gpuVendor}`) : formatNumber(null)}</td>
                  <td>{formatPercent(record.gpuAverageUtilizationPercent)}</td>
                  <td>{formatMetric(record.gpuAverageTemperatureCelsius, 'temperature')}</td>
                  <td>
                    <button className="button-secondary table-action" onClick={() => void openSample(record)} type="button">
                      {t('analytics.actions.details')}
                    </button>
                  </td>
                </tr>
              ))}
              {records.length === 0 ? (
                <tr>
                  <td colSpan="9">{t('analytics.emptyTable')}</td>
                </tr>
              ) : null}
            </tbody>
          </table>

          <div className="pagination-row">
            <button className="button-secondary" disabled={page <= 1} onClick={() => setPage((value) => Math.max(1, value - 1))} type="button">
              {t('analytics.actions.previous')}
            </button>
            <span className="pagination-status">
              {t('analytics.labels.pageStatus', {
                page: formatNumber(searchResult?.page || page),
                totalPages: formatNumber(totalPages),
                totalCount: formatNumber(searchResult?.totalCount || 0),
              })}
            </span>
            <button className="button-secondary" disabled={totalPages > 0 && page >= totalPages} onClick={() => setPage((value) => value + 1)} type="button">
              {t('analytics.actions.next')}
            </button>
          </div>
        </section>
      </div>

      {isModalOpen ? (
        <JsonModal
          closeLabel={modalCloseLabel}
          copiedLabel={t('overview.copyJsonSuccess')}
          copyLabel={t('overview.copyJson')}
          onClose={() => setIsModalOpen(false)}
          title={modalTitle}
          value={modalValue}
        />
      ) : null}
    </>
  )
}
