import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { copyTextToClipboard } from '../utils/clipboard'
import { SectionCard } from './SectionCard'

// Host-published ports from docker/compose.yaml. URLs use the host the browser already reached
// RigMonitor on, so they work locally and on a remote rig alike.
const services = [
  { key: 'grafana', port: 3000, path: '/', username: 'admin', password: 'admin' },
  { key: 'prometheus', port: 9090, path: '/' },
  { key: 'tempo', port: 3200, path: '/status' },
  { key: 'loki', port: 3100, path: '/ready' },
]

function resolveHost() {
  if (typeof window === 'undefined' || !window.location?.hostname) {
    return 'localhost'
  }

  return window.location.hostname
}

export function ExternalServicesCard({ className = '' }) {
  const { t } = useTranslation()
  const [copiedKey, setCopiedKey] = useState(null)
  const host = resolveHost()

  async function handleCopy(key, value) {
    try {
      await copyTextToClipboard(value)
      setCopiedKey(key)
      window.setTimeout(() => setCopiedKey((current) => (current === key ? null : current)), 1500)
    } catch {
      setCopiedKey(null)
    }
  }

  return (
    <SectionCard
      className={`external-services-card ${className}`.trim()}
      title={t('externalServices.title')}
      description={t('externalServices.description')}
    >
      <div className="table-wrap">
        <table className="metric-table external-services-table">
          <thead>
            <tr>
              <th>{t('externalServices.service')}</th>
              <th>{t('externalServices.url')}</th>
              <th>{t('externalServices.credentials')}</th>
            </tr>
          </thead>
          <tbody>
            {services.map((service) => {
              const url = `http://${host}:${service.port}${service.path === '/' ? '' : service.path}`
              const credentials = service.username ? `${service.username} / ${service.password}` : null

              return (
                <tr key={service.key}>
                  <td>
                    <strong>{t(`externalServices.names.${service.key}`)}</strong>
                    <div className="external-services-hint">{t(`externalServices.hints.${service.key}`)}</div>
                  </td>
                  <td>
                    <div className="external-services-value">
                      <a className="mono-token" href={url} rel="noreferrer" target="_blank">
                        {url}
                      </a>
                      <button
                        className="button-secondary external-services-copy"
                        onClick={() => handleCopy(`${service.key}-url`, url)}
                        type="button"
                      >
                        {copiedKey === `${service.key}-url` ? t('externalServices.copied') : t('externalServices.copy')}
                      </button>
                    </div>
                  </td>
                  <td>
                    {credentials ? (
                      <div className="external-services-value">
                        <code className="mono-token">{credentials}</code>
                        <button
                          className="button-secondary external-services-copy"
                          onClick={() => handleCopy(`${service.key}-credentials`, credentials)}
                          type="button"
                        >
                          {copiedKey === `${service.key}-credentials` ? t('externalServices.copied') : t('externalServices.copy')}
                        </button>
                      </div>
                    ) : (
                      <span className="external-services-hint">{t('externalServices.noCredentials')}</span>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <p className="external-services-footnote">{t('externalServices.footnote')}</p>
    </SectionCard>
  )
}
