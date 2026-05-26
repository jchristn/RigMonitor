import { useTranslation } from 'react-i18next'
import { formatDateTime, formatElapsedMs } from '../i18n/formatters'

function statusTone(statusCode) {
  if (statusCode === 'ok') {
    return 'success'
  }

  if (statusCode === 'stale' || statusCode === 'error') {
    return 'danger'
  }

  if (statusCode === 'unsupported' || statusCode === 'unavailable') {
    return 'warning'
  }

  return ''
}

function freshnessTone(statusCode) {
  if (statusCode === 'fresh') {
    return 'success'
  }

  if (statusCode === 'stale') {
    return 'danger'
  }

  if (statusCode === 'unknown') {
    return 'warning'
  }

  return ''
}

export function SectionCard({ title, description, children, extra = null, className = '', status = null }) {
  const { t } = useTranslation()
  const freshness = status?.freshness || null

  return (
    <section className={`surface section-card ${className}`.trim()}>
      <div className="section-header">
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        <div className="section-header-aside">
          {status ? (
            <div className="section-badges">
              <span className={`pill ${statusTone(status.statusCode)}`.trim()}>
                {`${t('collection.statusLabel')}: ${t(`collection.status.${status.statusCode}`)}`}
              </span>
              <span className={`pill ${freshnessTone(freshness?.status)}`.trim()}>
                {`${t('collection.freshnessLabel')}: ${t(`collection.freshness.${freshness?.status || 'unknown'}`)}`}
              </span>
            </div>
          ) : null}
          {extra}
        </div>
      </div>
      {status ? (
        <dl className="section-status-grid">
          <div className="section-status-item">
            <dt>{t('collection.requestLabel')}</dt>
            <dd>{status.requested ? t('collection.requested') : t('collection.notRequested')}</dd>
          </div>
          <div className="section-status-item">
            <dt>{t('collection.supportLabel')}</dt>
            <dd>{status.supported ? t('collection.supported') : t('collection.unsupported')}</dd>
          </div>
          <div className="section-status-item">
            <dt>{t('collection.lastSuccessLabel')}</dt>
            <dd>{formatDateTime(status.lastSuccessUtc)}</dd>
          </div>
          <div className="section-status-item">
            <dt>{t('collection.lastAttemptLabel')}</dt>
            <dd>{formatDateTime(status.lastAttemptUtc)}</dd>
          </div>
          <div className="section-status-item">
            <dt>{t('collection.lastDurationLabel')}</dt>
            <dd>{formatElapsedMs(status.lastDurationMs)}</dd>
          </div>
          <div className="section-status-item">
            <dt>{t('collection.freshnessLabel')}</dt>
            <dd>
              {t(`collection.freshness.${freshness?.status || 'unknown'}`)}
              {freshness?.ageMs != null ? ` (${formatElapsedMs(freshness.ageMs)})` : ''}
            </dd>
          </div>
          {status.message ? (
            <div className="section-status-item section-status-item-wide">
              <dt>{t('collection.detailLabel')}</dt>
              <dd>{status.message}</dd>
            </div>
          ) : null}
          {status.lastError ? (
            <div className="section-status-item section-status-item-wide">
              <dt>{t('collection.errorLabel')}</dt>
              <dd>{status.lastError}</dd>
            </div>
          ) : null}
        </dl>
      ) : null}
      {children}
    </section>
  )
}
