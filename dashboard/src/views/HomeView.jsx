import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { JsonModal } from '../components/JsonModal'
import { RefreshButton } from '../components/RefreshButton'
import { SectionCard } from '../components/SectionCard'
import { StatCard } from '../components/StatCard'
import { useTelemetry } from '../hooks/useTelemetry'
import {
  formatBytes,
  formatDateTime,
  formatDateTimeWithSeconds,
  formatDuration,
  formatDurationHms,
  formatNumber,
  formatPercent,
} from '../i18n/formatters'

const refreshIntervals = [
  { value: 2000, label: '2s' },
  { value: 5000, label: '5s' },
  { value: 10000, label: '10s' },
  { value: 30000, label: '30s' },
  { value: 60000, label: '60s' },
]

function megabytesToBytes(value) {
  if (value == null || Number.isNaN(value)) {
    return value
  }

  return value * 1024 * 1024
}

function telemetryAgeMs(collectedUtc, nowMs) {
  if (!collectedUtc) {
    return null
  }

  const collectedMs = Date.parse(collectedUtc)
  if (Number.isNaN(collectedMs)) {
    return null
  }

  return Math.max(0, nowMs - collectedMs)
}

function hasGpuMemory(metrics) {
  return (metrics?.memoryTotalMegabytes || 0) > 0
}

function gpuMemoryUtilization(metrics) {
  if (!hasGpuMemory(metrics)) {
    return '-'
  }

  return formatPercent(metrics.memoryUtilizationPercent)
}

function gpuMemoryRange(metrics, sharedLabel) {
  if (!hasGpuMemory(metrics)) {
    return '-'
  }

  const suffix = metrics.memoryShared ? ` (${sharedLabel})` : ''
  return `${formatBytes(megabytesToBytes(metrics.memoryUsedMegabytes))} / ${formatBytes(megabytesToBytes(metrics.memoryTotalMegabytes))}${suffix}`
}

function gpuMemorySummary(gpu, sharedLabel) {
  const devices = gpu?.devices || []
  if (devices.length < 1) {
    return '-'
  }

  const memoryDevices = devices.filter((device) => hasGpuMemory(device.metrics))
  if (memoryDevices.length < 1) {
    return '-'
  }

  const sharedDevice = memoryDevices.find((device) => device.metrics?.memoryShared)
  if (sharedDevice) {
    const metrics = sharedDevice.metrics
    return `${formatBytes(megabytesToBytes(metrics.memoryUsedMegabytes))} / ${formatBytes(megabytesToBytes(metrics.memoryTotalMegabytes))} (${formatPercent(metrics.memoryUtilizationPercent)}, ${sharedLabel})`
  }

  const usedMegabytes = memoryDevices.reduce((total, device) => total + (device.metrics?.memoryUsedMegabytes || 0), 0)
  const totalMegabytes = memoryDevices.reduce((total, device) => total + (device.metrics?.memoryTotalMegabytes || 0), 0)

  if (totalMegabytes <= 0) {
    return '-'
  }

  const utilizationPercent = (usedMegabytes / totalMegabytes) * 100
  return `${formatBytes(megabytesToBytes(usedMegabytes))} / ${formatBytes(megabytesToBytes(totalMegabytes))} (${formatPercent(utilizationPercent)})`
}

function StateBox({ title, body, className = '' }) {
  return (
    <section className={`surface state-box ${className}`.trim()}>
      <h2>{title}</h2>
      <p>{body}</p>
    </section>
  )
}

function SectionPlaceholder({ message }) {
  return (
    <div className="section-placeholder">
      <p>{message}</p>
    </div>
  )
}

export function HomeView() {
  const { t } = useTranslation()
  const [isJsonModalOpen, setIsJsonModalOpen] = useState(false)
  const [jsonModalValue, setJsonModalValue] = useState('')
  const [nowMs, setNowMs] = useState(() => Date.now())
  const {
    autoRefresh,
    capabilities,
    data,
    error,
    intervalMs,
    loading,
    refresh,
    setAutoRefresh,
    setIntervalMs,
  } = useTelemetry()

  useEffect(() => {
    const timerId = window.setInterval(() => {
      setNowMs(Date.now())
    }, 1000)

    return () => window.clearInterval(timerId)
  }, [])

  if (loading && !data) {
    return <StateBox title={t('overview.loadingTitle')} body={t('overview.loadingBody')} />
  }

  if (error && !data) {
    return <StateBox title={t('overview.errorTitle')} body={`${t('overview.errorBody')} ${error}`} />
  }

  if (!data) {
    return <StateBox title={t('overview.emptyTitle')} body={t('overview.emptyBody')} />
  }

  function openJsonModal() {
    setJsonModalValue(JSON.stringify(data, null, 2))
    setIsJsonModalOpen(true)
  }

  const collection = data.collection || {}
  const systemStatus = collection.system || null
  const cpuStatus = collection.cpu || null
  const memoryStatus = collection.memory || null
  const diskStatus = collection.disk || null
  const networkStatus = collection.network || null
  const gpuStatus = collection.gpu || null
  const ollamaStatus = collection.ollama || null
  const vllmStatus = collection.vllm || null
  const utilyzeStatus = collection.utilyze || null
  const collectedUtc = collection.collectedUtc || data.collectedUtc
  const ageMs = telemetryAgeMs(collectedUtc, nowMs)

  function placeholderMessage(status) {
    return status?.message || t('states.sectionPayloadMissing')
  }

  function labelsText(labels) {
    const entries = Object.entries(labels || {})
    if (entries.length < 1) {
      return '-'
    }

    return entries.map(([key, value]) => `${key}=${value}`).join(', ')
  }

  function vllmCapabilityLabel() {
    if (!capabilities?.vllmEnabled) {
      return t('overview.capabilities.vllmDisabled')
    }

    return capabilities?.vllmAvailable
      ? t('overview.capabilities.vllmOnline')
      : t('overview.capabilities.vllmOffline')
  }

  function vllmCapabilityTone() {
    if (!capabilities?.vllmEnabled) {
      return 'warning'
    }

    return capabilities?.vllmAvailable ? 'success' : 'warning'
  }

  function utilyzeCapabilityLabel() {
    if (!capabilities?.utilyzeEnabled) {
      return t('overview.capabilities.utilyzeDisabled')
    }

    return capabilities?.utilyzeAvailable
      ? t('overview.capabilities.utilyzeOnline')
      : t('overview.capabilities.utilyzeOffline')
  }

  function utilyzeCapabilityTone() {
    if (!capabilities?.utilyzeEnabled) {
      return 'warning'
    }

    return capabilities?.utilyzeAvailable ? 'success' : 'warning'
  }

  return (
    <>
      <div className="view-grid">
        <section className="surface toolbar">
          <div>
            <h2>{t('overview.title')}</h2>
            <p>{t('overview.subtitle')}</p>
          </div>
          <div className="toolbar-actions">
            <button className="button-secondary" type="button" onClick={openJsonModal}>
              {t('overview.viewJson')}
            </button>
            <RefreshButton label={t('overview.refresh')} onClick={() => void refresh()} variant="button-primary" />
            <label className="toggle">
              <span>{t('overview.refreshInterval')}</span>
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
              <span>{t('overview.autoRefresh')}</span>
            </label>
          </div>
        </section>

        {error ? (
          <div className="inline-alert warning" role="status">
            <strong>{t('overview.refreshErrorTitle')}</strong>
            <span>{`${t('overview.refreshErrorBody')} ${error}`}</span>
          </div>
        ) : null}

        <div className="pill-row">
          <span className={`pill ${capabilities?.nvidiaAvailable ? 'success' : 'warning'}`}>
            {capabilities?.nvidiaAvailable ? t('overview.capabilities.gpuOn') : t('overview.capabilities.gpuOff')}
          </span>
          <span className={`pill ${capabilities?.ollamaAvailable ? 'success' : 'warning'}`}>
            {capabilities?.ollamaAvailable ? t('overview.capabilities.ollamaOn') : t('overview.capabilities.ollamaOff')}
          </span>
          <span className={`pill ${vllmCapabilityTone()}`}>
            {vllmCapabilityLabel()}
          </span>
          <span className={`pill ${utilyzeCapabilityTone()}`}>
            {utilyzeCapabilityLabel()}
          </span>
        </div>

        <div className="stats-grid">
          <StatCard label={t('overview.cards.collected')} value={formatDateTimeWithSeconds(collectedUtc)} />
          <StatCard label={t('overview.cards.telemetryAge')} value={formatDurationHms(ageMs)} />
          <StatCard label={t('overview.cards.platform')} value={data.hostPlatform} />
          <StatCard label={t('overview.cards.uptime')} value={formatDuration(data.system?.uptimeMs)} />
          <StatCard label={t('overview.cards.memory')} value={formatPercent(data.memory?.utilizationPercent)} />
          <StatCard label={t('overview.cards.gpuMemory')} value={gpuMemorySummary(data.gpu, t('labels.shared'))} />
          <StatCard label={t('overview.cards.cpu')} value={formatPercent(data.cpu?.utilizationPercent)} />
        </div>

        <div className="overview-grid">
          <SectionCard className="overview-half" title={t('sections.system')} description={t('descriptions.system')} status={systemStatus}>
            {data.system ? (
              <table className="metric-table">
                <tbody>
                  <tr><th>{t('labels.hostname')}</th><td>{data.system.hostname || '-'}</td></tr>
                  <tr><th>{t('labels.os')}</th><td>{data.system.osDescription || '-'}</td></tr>
                  <tr><th>{t('labels.architecture')}</th><td>{data.system.osArchitecture || '-'}</td></tr>
                  <tr><th>{t('labels.processArchitecture')}</th><td>{data.system.processArchitecture || '-'}</td></tr>
                </tbody>
              </table>
            ) : (
              <SectionPlaceholder message={placeholderMessage(systemStatus)} />
            )}
          </SectionCard>

          <SectionCard className="overview-half" title={t('sections.cpu')} description={t('descriptions.cpu')} status={cpuStatus}>
            {data.cpu ? (
              <table className="metric-table">
                <tbody>
                  <tr><th>{t('labels.logicalCores')}</th><td>{formatNumber(data.cpu.logicalCoreCount)}</td></tr>
                  <tr><th>{t('labels.utilization')}</th><td>{formatPercent(data.cpu.utilizationPercent)}</td></tr>
                </tbody>
              </table>
            ) : (
              <SectionPlaceholder message={placeholderMessage(cpuStatus)} />
            )}
          </SectionCard>

          <SectionCard className="overview-half" title={t('sections.memory')} description={t('descriptions.memory')} status={memoryStatus}>
            {data.memory ? (
              <table className="metric-table">
                <tbody>
                  <tr><th>{t('labels.total')}</th><td>{formatBytes(data.memory.totalBytes)}</td></tr>
                  <tr><th>{t('labels.available')}</th><td>{formatBytes(data.memory.availableBytes)}</td></tr>
                  <tr><th>{t('labels.used')}</th><td>{formatBytes(data.memory.usedBytes)}</td></tr>
                  <tr><th>{t('labels.utilization')}</th><td>{formatPercent(data.memory.utilizationPercent)}</td></tr>
                </tbody>
              </table>
            ) : (
              <SectionPlaceholder message={placeholderMessage(memoryStatus)} />
            )}
          </SectionCard>

          <SectionCard className="overview-half" title={t('sections.disk')} description={t('descriptions.disk')} status={diskStatus}>
            {data.disk ? (
              <>
                <div className="pill-row">
                  <span className="pill">{`${t('labels.reads')}: ${formatNumber(data.disk.readOperationsPerSecond, { maximumFractionDigits: 2 })}`}</span>
                  <span className="pill">{`${t('labels.writes')}: ${formatNumber(data.disk.writeOperationsPerSecond, { maximumFractionDigits: 2 })}`}</span>
                  <span className="pill">{`${t('labels.readQueue')}: ${formatNumber(data.disk.readQueueDepth, { maximumFractionDigits: 2 })}`}</span>
                  <span className="pill">{`${t('labels.writeQueue')}: ${formatNumber(data.disk.writeQueueDepth, { maximumFractionDigits: 2 })}`}</span>
                </div>
                <table className="metric-table">
                  <thead>
                    <tr>
                      <th>{t('labels.volume')}</th>
                      <th>{t('labels.mountPoint')}</th>
                      <th>{t('labels.used')}</th>
                      <th>{t('labels.total')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(data.disk.volumes || []).map((volume) => (
                      <tr key={`${volume.name}-${volume.mountPoint}`}>
                        <td>{volume.name}</td>
                        <td>{volume.mountPoint}</td>
                        <td>{`${formatBytes(volume.usedBytes)} (${formatPercent(volume.utilizationPercent)})`}</td>
                        <td>{formatBytes(volume.totalBytes)}</td>
                      </tr>
                    ))}
                    {(data.disk.volumes || []).length === 0 ? (
                      <tr>
                        <td colSpan="4">{t('states.none')}</td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </>
            ) : (
              <SectionPlaceholder message={placeholderMessage(diskStatus)} />
            )}
          </SectionCard>

          <SectionCard
            className="overview-span-2"
            title={t('sections.vllm')}
            description={t('descriptions.vllm')}
            extra={data.vllm ? <span className="pill success">{`${t('labels.endpoint')}: ${data.vllm.metricsEndpoint || '-'}`}</span> : null}
            status={vllmStatus}
          >
            {data.vllm ? (
              <>
              <div className="stats-grid">
                <StatCard label={t('labels.runningRequests')} value={formatNumber(data.vllm.summary?.runningRequests)} />
                <StatCard label={t('labels.waitingRequests')} value={formatNumber(data.vllm.summary?.waitingRequests)} />
                <StatCard label={t('labels.gpuCache')} value={formatPercent(data.vllm.summary?.gpuCacheUsagePercent)} />
                <StatCard label={t('labels.models')} value={formatNumber((data.vllm.modelNames || []).length)} />
                <StatCard label={t('labels.promptTokens')} value={formatNumber(data.vllm.summary?.promptTokensTotal)} />
                <StatCard label={t('labels.generationTokens')} value={formatNumber(data.vllm.summary?.generationTokensTotal)} />
                <StatCard label={t('labels.successfulRequests')} value={formatNumber(data.vllm.summary?.successfulRequestsTotal)} />
                <StatCard label={t('labels.metrics')} value={formatNumber((data.vllm.metrics || []).length)} />
              </div>

              <div className="subsection-block">
                <div className="subsection-header">
                  <div>
                    <h3>{t('labels.models')}</h3>
                    <p>{t('descriptions.vllmModels')}</p>
                  </div>
                  <span className="pill">{formatNumber((data.vllm.modelNames || []).length)}</span>
                </div>
                <div className="pill-row">
                  {(data.vllm.modelNames || []).map((model) => (
                    <span className="pill" key={model}>{model}</span>
                  ))}
                  {(data.vllm.modelNames || []).length === 0 ? (
                    <span className="pill">{t('states.none')}</span>
                  ) : null}
                </div>
              </div>

              <div className="subsection-block">
                <div className="subsection-header">
                  <div>
                    <h3>{t('labels.metrics')}</h3>
                    <p>{t('descriptions.vllmMetrics')}</p>
                  </div>
                  <span className="pill">{formatNumber((data.vllm.metrics || []).length)}</span>
                </div>
                <table className="metric-table">
                  <thead>
                    <tr>
                      <th>{t('labels.name')}</th>
                      <th>{t('labels.value')}</th>
                      <th>{t('labels.labels')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(data.vllm.metrics || []).slice(0, 50).map((metric, index) => (
                      <tr key={`${metric.name}-${index}`}>
                        <td>{metric.name}</td>
                        <td>{formatNumber(metric.value, { maximumFractionDigits: 4 })}</td>
                        <td>{labelsText(metric.labels)}</td>
                      </tr>
                    ))}
                    {(data.vllm.metrics || []).length === 0 ? (
                      <tr>
                        <td colSpan="3">{t('states.none')}</td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
              </>
            ) : (
              <SectionPlaceholder message={placeholderMessage(vllmStatus)} />
            )}
          </SectionCard>

          <SectionCard
            className="overview-span-2"
            title={t('sections.network')}
            description={t('descriptions.network')}
            extra={data.network ? <span className="pill">{`${t('labels.activeInterfaces')}: ${formatNumber(data.network.activeInterfaceCount)}`}</span> : null}
            status={networkStatus}
          >
            {data.network ? (
              <table className="metric-table">
                <thead>
                  <tr>
                    <th>{t('labels.name')}</th>
                    <th>{t('labels.rx')}</th>
                    <th>{t('labels.tx')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(data.network.interfaces || []).map((item) => (
                    <tr key={item.name}>
                      <td>{item.name}</td>
                      <td>{`${formatBytes(item.receiveBytesPerSecond)}/s`}</td>
                      <td>{`${formatBytes(item.transmitBytesPerSecond)}/s`}</td>
                    </tr>
                  ))}
                  {(data.network.interfaces || []).length === 0 ? (
                    <tr>
                      <td colSpan="3">{t('states.none')}</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            ) : (
              <SectionPlaceholder message={placeholderMessage(networkStatus)} />
            )}
          </SectionCard>

          <SectionCard
            className="overview-half"
            title={t('sections.gpu')}
            description={t('descriptions.gpu')}
            extra={data.gpu ? <span className="pill success">{`${t('labels.vendor')}: ${data.gpu.vendor}`}</span> : null}
            status={gpuStatus}
          >
            {data.gpu ? (
              <>
              <div className="pill-row">
                <span className="pill">{`${t('labels.endpoint')}: ${data.gpu.exporterEndpoint}`}</span>
              </div>
              <table className="metric-table">
                <thead>
                  <tr>
                    <th>{t('labels.model')}</th>
                    <th>{t('labels.vram')}</th>
                    <th>{t('labels.vramUtilization')}</th>
                    <th>{t('labels.utilization')}</th>
                    <th>{t('labels.temperature')}</th>
                    <th>{t('labels.power')}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.gpu.devices.map((device) => (
                    <tr key={`${device.deviceIndex}-${device.uuid}`}>
                      <td>{device.model || device.uuid}</td>
                      <td>{gpuMemoryRange(device.metrics, t('labels.shared'))}</td>
                      <td>{gpuMemoryUtilization(device.metrics)}</td>
                      <td>{formatPercent(device.metrics?.gpuUtilizationPercent)}</td>
                      <td>{`${formatNumber(device.metrics?.temperatureCelsius, { maximumFractionDigits: 1 })} C`}</td>
                      <td>{`${formatNumber(device.metrics?.powerUsageWatts, { maximumFractionDigits: 1 })} W`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              </>
            ) : (
              <SectionPlaceholder message={placeholderMessage(gpuStatus)} />
            )}
          </SectionCard>

          <SectionCard
            className="overview-span-2"
            title={t('sections.utilyze')}
            description={t('descriptions.utilyze')}
            extra={data.utilyze ? <span className="pill success">{`${t('labels.endpoint')}: ${data.utilyze.endpoint || '-'}`}</span> : null}
            status={utilyzeStatus}
          >
            {data.utilyze ? (
              <>
              <div className="stats-grid">
                <StatCard label={t('labels.devices')} value={formatNumber((data.utilyze.deviceIds || []).length)} />
                <StatCard label={t('labels.endpoint')} value={data.utilyze.endpoint || '-'} />
                <StatCard label={t('labels.collected')} value={formatDateTime(data.utilyze.collectedUtc)} />
                <StatCard label={t('labels.status')} value={data.utilyze.available ? t('states.online') : t('states.offline')} />
              </div>
              <table className="metric-table">
                <thead>
                  <tr>
                    <th>{t('labels.device')}</th>
                    <th>{t('labels.computeSol')}</th>
                    <th>{t('labels.memorySol')}</th>
                    <th>{t('labels.smActive')}</th>
                    <th>{t('labels.nvmlUtilization')}</th>
                    <th>{t('labels.pcie')}</th>
                    <th>{t('labels.nvlink')}</th>
                    <th>{t('labels.model')}</th>
                    <th>{t('labels.computeCeiling')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(data.utilyze.devices || []).map((device) => (
                    <tr key={device.deviceIndex}>
                      <td>{`${t('labels.gpu')} ${formatNumber(device.deviceIndex)}`}</td>
                      <td>{formatPercent(device.computeSolPercent)}</td>
                      <td>{formatPercent(device.memorySolPercent)}</td>
                      <td>{formatPercent(device.smActivePercent)}</td>
                      <td>{formatPercent(device.nvmlUtilizationPercent)}</td>
                      <td>{`${formatBytes(device.pcieReceiveBytesPerSecond)}/s ${t('labels.rx')} / ${formatBytes(device.pcieTransmitBytesPerSecond)}/s ${t('labels.tx')}`}</td>
                      <td>{`${formatBytes(device.nvlinkReceiveBytesPerSecond)}/s ${t('labels.rx')} / ${formatBytes(device.nvlinkTransmitBytesPerSecond)}/s ${t('labels.tx')}`}</td>
                      <td>{device.modelName || '-'}</td>
                      <td>{formatPercent(device.computeSolCeilingPercent)}</td>
                    </tr>
                  ))}
                  {(data.utilyze.devices || []).length === 0 ? (
                    <tr>
                      <td colSpan="9">{t('states.none')}</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
              </>
            ) : (
              <SectionPlaceholder message={placeholderMessage(utilyzeStatus)} />
            )}
          </SectionCard>

          <SectionCard
            className="overview-span-2"
            title={t('sections.ollama')}
            description={t('descriptions.ollama')}
            extra={data.ollama ? <span className="pill success">{`${t('labels.version')}: ${data.ollama.version || '-'}`}</span> : null}
            status={ollamaStatus}
          >
            {data.ollama ? (
              <>
              <div className="stats-grid">
                <StatCard label={t('labels.availableModels')} value={formatNumber(data.ollama.availableModelCount)} />
                <StatCard label={t('labels.loadedModels')} value={formatNumber(data.ollama.loadedModelCount)} />
                <StatCard label={t('labels.endpoint')} value={data.ollama.baseUrl || '-'} />
                <StatCard label={t('labels.version')} value={data.ollama.version || '-'} />
              </div>

              <div className="subsection-block">
                <div className="subsection-header">
                  <div>
                    <h3>{t('labels.loadedModels')}</h3>
                    <p>{t('descriptions.loadedModels')}</p>
                  </div>
                  <span className="pill">{formatNumber(data.ollama.loadedModelCount)}</span>
                </div>
                <table className="metric-table">
                  <thead>
                    <tr>
                      <th>{t('labels.model')}</th>
                      <th>{t('labels.total')}</th>
                      <th>{t('labels.used')}</th>
                      <th>{t('labels.expires')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(data.ollama.loadedModels || []).map((model) => (
                      <tr key={`${model.model}-${model.digest}`}>
                        <td>{model.name}</td>
                        <td>{formatBytes(model.sizeBytes)}</td>
                        <td>{formatBytes(model.sizeVramBytes)}</td>
                        <td>{formatDateTime(model.expiresAtUtc)}</td>
                      </tr>
                    ))}
                    {(data.ollama.loadedModels || []).length === 0 ? (
                      <tr>
                        <td colSpan="4">{t('states.none')}</td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>

              <div className="subsection-block">
                <div className="subsection-header">
                  <div>
                    <h3>{t('labels.availableModels')}</h3>
                    <p>{t('descriptions.availableModels')}</p>
                  </div>
                  <span className="pill">{formatNumber(data.ollama.availableModelCount)}</span>
                </div>
                <table className="metric-table">
                  <thead>
                    <tr>
                      <th>{t('labels.model')}</th>
                      <th>{t('labels.total')}</th>
                      <th>{t('labels.quantization')}</th>
                      <th>{t('labels.parameters')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(data.ollama.availableModels || []).map((model) => (
                      <tr key={`${model.model}-${model.digest}`}>
                        <td>{model.name}</td>
                        <td>{formatBytes(model.sizeBytes)}</td>
                        <td>{model.quantizationLevel || '-'}</td>
                        <td>{model.parameterSize || '-'}</td>
                      </tr>
                    ))}
                    {(data.ollama.availableModels || []).length === 0 ? (
                      <tr>
                        <td colSpan="4">{t('states.none')}</td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
              </>
            ) : (
              <SectionPlaceholder message={placeholderMessage(ollamaStatus)} />
            )}
          </SectionCard>
        </div>
      </div>

      {isJsonModalOpen ? (
        <JsonModal
          closeLabel={t('overview.closeJson')}
          copiedLabel={t('overview.copyJsonSuccess')}
          copyLabel={t('overview.copyJson')}
          onClose={() => setIsJsonModalOpen(false)}
          title={t('overview.jsonTitle')}
          value={jsonModalValue}
        />
      ) : null}
    </>
  )
}
