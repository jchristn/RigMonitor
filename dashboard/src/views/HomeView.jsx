import { useTranslation } from 'react-i18next'
import { SectionCard } from '../components/SectionCard'
import { StatCard } from '../components/StatCard'
import { useTelemetry } from '../hooks/useTelemetry'
import { formatBytes, formatDateTime, formatDuration, formatNumber, formatPercent } from '../i18n/formatters'

const refreshIntervals = [
  { value: 2000, label: '2s' },
  { value: 5000, label: '5s' },
  { value: 10000, label: '10s' },
  { value: 30000, label: '30s' },
  { value: 60000, label: '60s' },
]

function StateBox({ title, body, className = '' }) {
  return (
    <section className={`surface state-box ${className}`.trim()}>
      <h2>{title}</h2>
      <p>{body}</p>
    </section>
  )
}

export function HomeView() {
  const { t } = useTranslation()
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

  if (loading) {
    return <StateBox title={t('overview.loadingTitle')} body={t('overview.loadingBody')} />
  }

  if (error) {
    return <StateBox title={t('overview.errorTitle')} body={`${t('overview.errorBody')} ${error}`} />
  }

  if (!data) {
    return <StateBox title={t('overview.emptyTitle')} body={t('overview.emptyBody')} />
  }

  return (
    <div className="view-grid">
      <section className="surface toolbar">
        <div>
          <h2>{t('overview.title')}</h2>
          <p>{t('overview.subtitle')}</p>
        </div>
        <div className="toolbar-actions">
          <button className="button-primary" type="button" onClick={() => void refresh()}>
            {t('overview.refresh')}
          </button>
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

      <div className="pill-row">
        <span className={`pill ${capabilities?.nvidiaAvailable ? 'success' : 'warning'}`}>
          {capabilities?.nvidiaAvailable ? t('overview.capabilities.gpuOn') : t('overview.capabilities.gpuOff')}
        </span>
        <span className={`pill ${capabilities?.ollamaAvailable ? 'success' : 'warning'}`}>
          {capabilities?.ollamaAvailable ? t('overview.capabilities.ollamaOn') : t('overview.capabilities.ollamaOff')}
        </span>
      </div>

      <div className="stats-grid">
        <StatCard label={t('overview.cards.platform')} value={data.hostPlatform} />
        <StatCard label={t('overview.cards.uptime')} value={formatDuration(data.system?.uptimeMs)} />
        <StatCard label={t('overview.cards.memory')} value={formatPercent(data.memory?.utilizationPercent)} />
        <StatCard label={t('overview.cards.cpu')} value={formatPercent(data.cpu?.utilizationPercent)} />
      </div>

      <div className="overview-grid">
        <SectionCard className="overview-half" title={t('sections.system')} description={t('descriptions.system')}>
          <table className="metric-table">
            <tbody>
              <tr><th>{t('labels.hostname')}</th><td>{data.system?.hostname || '-'}</td></tr>
              <tr><th>{t('labels.os')}</th><td>{data.system?.osDescription || '-'}</td></tr>
              <tr><th>{t('labels.architecture')}</th><td>{data.system?.osArchitecture || '-'}</td></tr>
              <tr><th>{t('labels.processArchitecture')}</th><td>{data.system?.processArchitecture || '-'}</td></tr>
            </tbody>
          </table>
        </SectionCard>

        <SectionCard className="overview-half" title={t('sections.cpu')} description={t('descriptions.cpu')}>
          <table className="metric-table">
            <tbody>
              <tr><th>{t('labels.logicalCores')}</th><td>{formatNumber(data.cpu?.logicalCoreCount)}</td></tr>
              <tr><th>{t('labels.utilization')}</th><td>{formatPercent(data.cpu?.utilizationPercent)}</td></tr>
            </tbody>
          </table>
        </SectionCard>

        <SectionCard className="overview-half" title={t('sections.memory')} description={t('descriptions.memory')}>
          <table className="metric-table">
            <tbody>
              <tr><th>{t('labels.total')}</th><td>{formatBytes(data.memory?.totalBytes)}</td></tr>
              <tr><th>{t('labels.available')}</th><td>{formatBytes(data.memory?.availableBytes)}</td></tr>
              <tr><th>{t('labels.used')}</th><td>{formatBytes(data.memory?.usedBytes)}</td></tr>
              <tr><th>{t('labels.utilization')}</th><td>{formatPercent(data.memory?.utilizationPercent)}</td></tr>
            </tbody>
          </table>
        </SectionCard>

        <SectionCard className="overview-half" title={t('sections.disk')} description={t('descriptions.disk')}>
          <div className="pill-row">
            <span className="pill">{`${t('labels.reads')}: ${formatNumber(data.disk?.readOperationsPerSecond, { maximumFractionDigits: 2 })}`}</span>
            <span className="pill">{`${t('labels.writes')}: ${formatNumber(data.disk?.writeOperationsPerSecond, { maximumFractionDigits: 2 })}`}</span>
            <span className="pill">{`${t('labels.readQueue')}: ${formatNumber(data.disk?.readQueueDepth, { maximumFractionDigits: 2 })}`}</span>
            <span className="pill">{`${t('labels.writeQueue')}: ${formatNumber(data.disk?.writeQueueDepth, { maximumFractionDigits: 2 })}`}</span>
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
              {(data.disk?.volumes || []).map((volume) => (
                <tr key={`${volume.name}-${volume.mountPoint}`}>
                  <td>{volume.name}</td>
                  <td>{volume.mountPoint}</td>
                  <td>{`${formatBytes(volume.usedBytes)} (${formatPercent(volume.utilizationPercent)})`}</td>
                  <td>{formatBytes(volume.totalBytes)}</td>
                </tr>
              ))}
              {(data.disk?.volumes || []).length === 0 ? (
                <tr>
                  <td colSpan="4">{t('states.none')}</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </SectionCard>

        <SectionCard
          className="overview-span-2"
          title={t('sections.network')}
          description={t('descriptions.network')}
          extra={<span className="pill">{`${t('labels.activeInterfaces')}: ${formatNumber(data.network?.activeInterfaceCount)}`}</span>}
        >
          <table className="metric-table">
            <thead>
              <tr>
                <th>{t('labels.name')}</th>
                <th>{t('labels.rx')}</th>
                <th>{t('labels.tx')}</th>
              </tr>
            </thead>
            <tbody>
              {(data.network?.interfaces || []).map((item) => (
                <tr key={item.name}>
                  <td>{item.name}</td>
                  <td>{`${formatBytes(item.receiveBytesPerSecond)}/s`}</td>
                  <td>{`${formatBytes(item.transmitBytesPerSecond)}/s`}</td>
                </tr>
              ))}
              {(data.network?.interfaces || []).length === 0 ? (
                <tr>
                  <td colSpan="3">{t('states.none')}</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </SectionCard>

        {data.gpu ? (
          <SectionCard
            className="overview-half"
            title={t('sections.gpu')}
            description={t('descriptions.gpu')}
            extra={<span className="pill success">{`${t('labels.vendor')}: ${data.gpu.vendor}`}</span>}
          >
            <div className="pill-row">
              <span className="pill">{`${t('labels.endpoint')}: ${data.gpu.exporterEndpoint}`}</span>
            </div>
            <table className="metric-table">
              <thead>
                <tr>
                  <th>{t('labels.model')}</th>
                  <th>{t('labels.utilization')}</th>
                  <th>{t('labels.temperature')}</th>
                  <th>{t('labels.power')}</th>
                </tr>
              </thead>
              <tbody>
                {data.gpu.devices.map((device) => (
                  <tr key={`${device.deviceIndex}-${device.uuid}`}>
                    <td>{device.model || device.uuid}</td>
                    <td>{formatPercent(device.metrics?.gpuUtilizationPercent)}</td>
                    <td>{`${formatNumber(device.metrics?.temperatureCelsius, { maximumFractionDigits: 1 })} C`}</td>
                    <td>{`${formatNumber(device.metrics?.powerUsageWatts, { maximumFractionDigits: 1 })} W`}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </SectionCard>
        ) : (
          <StateBox className="overview-half" title={t('sections.gpu')} body={t('states.gpuMissing')} />
        )}

        {data.ollama ? (
          <SectionCard
            className="overview-span-2"
            title={t('sections.ollama')}
            description={t('descriptions.ollama')}
            extra={<span className="pill success">{`${t('labels.version')}: ${data.ollama.version || '-'}`}</span>}
          >
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
          </SectionCard>
        ) : (
          <StateBox className="overview-span-2" title={t('sections.ollama')} body={t('states.ollamaMissing')} />
        )}
      </div>
    </div>
  )
}
