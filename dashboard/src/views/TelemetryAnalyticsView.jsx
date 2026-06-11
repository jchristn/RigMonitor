import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { JsonModal } from '../components/JsonModal'
import { StatCard } from '../components/StatCard'
import {
  formatBytes,
  formatDateTime,
  formatDateTimeWithSeconds,
  formatNumber,
  formatPercent,
} from '../i18n/formatters'
import {
  getTelemetryHistoryStatus,
  getTelemetrySample,
  rollupTelemetryHistory,
  searchTelemetryHistory,
} from '../utils/api'

const metricDescriptors = [
  { key: 'cpu', field: 'averageCpuUtilizationPercent', recordField: 'cpuUtilizationPercent', labelKey: 'analytics.metrics.cpu', type: 'percent' },
  { key: 'memory', field: 'averageMemoryUtilizationPercent', recordField: 'memoryUtilizationPercent', labelKey: 'analytics.metrics.memory', type: 'percent' },
  { key: 'gpu', field: 'averageGpuUtilizationPercent', recordField: 'gpuAverageUtilizationPercent', labelKey: 'analytics.metrics.gpu', type: 'percent' },
  { key: 'gpuMemory', field: 'averageGpuMemoryUtilizationPercent', recordField: 'gpuAverageMemoryUtilizationPercent', labelKey: 'analytics.metrics.gpuMemory', type: 'percent' },
  { key: 'gpuTemperature', field: 'averageGpuTemperatureCelsius', recordField: 'gpuAverageTemperatureCelsius', labelKey: 'analytics.metrics.gpuTemperature', type: 'temperature' },
  { key: 'gpuPower', field: 'averageGpuPowerUsageWatts', recordField: 'gpuTotalPowerUsageWatts', labelKey: 'analytics.metrics.gpuPower', type: 'watts' },
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

const bucketOptions = [1, 15, 60, 360, 1440]
const pageSizeOptions = [10, 25, 50, 100]
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
    nvidiaAvailable: '',
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
    nvidiaAvailable: parseBoolean(filters.nvidiaAvailable),
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
    startUtc: parseDateInput(startInput),
    endUtc: parseDateInput(endInput),
    bucketMinutes,
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

  return formatNumber(value, { maximumFractionDigits: 2 })
}

function weightedAverage(buckets, field) {
  let total = 0
  let count = 0

  buckets.forEach((bucket) => {
    const value = bucket[field]
    const sampleCount = bucket.sampleCount || 0
    if (value != null && sampleCount > 0) {
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

function RollupChart({ buckets, descriptor, emptyLabel, label, metricLabel, sampleLabel, timeLabel }) {
  const { t } = useTranslation()
  const [hoveredPoint, setHoveredPoint] = useState(null)
  const width = 920
  const height = 585
  const padLeft = 64
  const padRight = 18
  const padTop = 18
  const padBottom = 40
  const plotWidth = width - padLeft - padRight
  const plotHeight = height - padTop - padBottom
  const values = buckets.map((bucket) => bucket[descriptor.field]).filter((value) => value != null)
  const hasData = buckets.length > 0 && values.length > 0
  const maxValue = values.length > 0 ? Math.max(...values, 1) : 1
  const yTickCount = hasData ? 5 : 2
  const yTicks = Array.from({ length: yTickCount }, (_, index) => {
    const ratio = yTickCount === 1 ? 0 : index / (yTickCount - 1)
    const value = maxValue * (1 - ratio)
    return {
      value,
      y: padTop + ratio * plotHeight,
    }
  })
  const points = buckets.map((bucket, index) => {
    const divisor = Math.max(1, buckets.length - 1)
    const value = bucket[descriptor.field] == null ? 0 : bucket[descriptor.field]
    const x = padLeft + (index / divisor) * plotWidth
    const y = padTop + plotHeight - (value / maxValue) * plotHeight
    return { x, y, value, bucket }
  })
  const hoverZones = points.map((point, index) => {
    const previousX = index === 0 ? padLeft : (points[index - 1].x + point.x) / 2
    const nextX = index === points.length - 1 ? padLeft + plotWidth : (point.x + points[index + 1].x) / 2
    return {
      point,
      x: previousX,
      width: nextX - previousX,
    }
  })
  const polyline = points.map((point) => `${point.x},${point.y}`).join(' ')
  const area = `${padLeft},${padTop + plotHeight} ${polyline} ${padLeft + plotWidth},${padTop + plotHeight}`
  const firstBucket = buckets[0]
  const lastBucket = buckets[buckets.length - 1]
  const tooltipRows = hoveredPoint ? buildTooltipRows(hoveredPoint, descriptor, t) : []
  const tooltipWidth = 300
  const tooltipHeight = 68 + tooltipRows.length * 15
  const tooltipX = hoveredPoint
    ? (hoveredPoint.x + tooltipWidth + 14 > width - padRight ? hoveredPoint.x - tooltipWidth - 14 : hoveredPoint.x + 14)
    : 0
  const tooltipY = hoveredPoint
    ? Math.min(Math.max(padTop + 6, hoveredPoint.y - 36), height - padBottom - tooltipHeight - 6)
    : 0

  return (
    <div className="chart-scroll">
      <svg
        aria-label={label}
        className="rollup-chart"
        onMouseLeave={() => setHoveredPoint(null)}
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
            <polygon className="chart-area" points={area} />
            <polyline className="chart-line" points={polyline} />
            {points.map((point) => (
              <circle className="chart-point" cx={point.x} cy={point.y} key={`${point.bucket.bucketStartUtc}-${point.x}`} r="1.4" />
            ))}
            {hoverZones.map((zone) => (
              <rect
                className="chart-hover-target"
                height={plotHeight}
                key={`${zone.point.bucket.bucketStartUtc}-${zone.x}`}
                onMouseEnter={() => setHoveredPoint(zone.point)}
                onMouseMove={() => setHoveredPoint(zone.point)}
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
        {hoveredPoint ? (
          <g className="chart-tooltip" pointerEvents="none">
            <line className="chart-tooltip-line" x1={hoveredPoint.x} x2={hoveredPoint.x} y1={padTop} y2={padTop + plotHeight} />
            <circle className="chart-active-point" cx={hoveredPoint.x} cy={hoveredPoint.y} r="2.25" />
            <rect className="chart-tooltip-box" height={tooltipHeight} rx="6" width={tooltipWidth} x={tooltipX} y={tooltipY} />
            <text className="chart-tooltip-text" x={tooltipX + 10} y={tooltipY + 16}>
              <tspan className="chart-tooltip-title">{metricLabel}</tspan>
              <tspan x={tooltipX + 10} dy="15">{formatMetric(hoveredPoint.value, descriptor.type)}</tspan>
              {tooltipRows.map((row, index) => (
                <tspan key={`${row}-${index}`} x={tooltipX + 10} dy="15">{row}</tspan>
              ))}
              <tspan x={tooltipX + 10} dy="15">{`${timeLabel}: ${formatDateTimeWithSeconds(hoveredPoint.bucket.bucketStartUtc)}`}</tspan>
              <tspan x={tooltipX + 10} dy="15">{`${sampleLabel}: ${formatNumber(hoveredPoint.bucket.sampleCount || 0)}`}</tspan>
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
            <button className="button-secondary" onClick={() => setRefreshNonce((value) => value + 1)} type="button">
              {t('analytics.actions.refresh')}
            </button>
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
                {metricDescriptors.map((item) => (
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
          </div>

          <div className="filter-grid compact">
            {['nvidiaAvailable', 'ollamaAvailable', 'vllmAvailable', 'utilyzeAvailable'].map((name) => (
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
                  <td colSpan="8">{t('analytics.emptyTable')}</td>
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
