import { useTranslation } from 'react-i18next'
import { formatMegabytes, hasNumber } from '../../i18n/formatters'

function percentOf(value, total) {
  if (!hasNumber(value) || !hasNumber(total) || total <= 0) {
    return 0
  }

  return Math.min(100, Math.max(0, (value / total) * 100))
}

export function UnifiedMemoryBar({ usedMegabytes, limitMegabytes, limitLabel, totalMegabytes }) {
  const { t } = useTranslation()
  if (!hasNumber(totalMegabytes) || totalMegabytes <= 0) {
    return null
  }

  const usedPercent = percentOf(usedMegabytes, totalMegabytes)
  const limitPercent = percentOf(limitMegabytes, totalMegabytes)

  return (
    <div className="unified-memory" data-testid="unified-memory-bar">
      <div className="unified-memory-legend">
        <span><i className="unified-memory-swatch used" />{`${t('labels.gpuMemoryInUse')}: ${formatMegabytes(usedMegabytes)}`}</span>
        {hasNumber(limitMegabytes) ? (
          <span><i className="unified-memory-swatch limit" />{`${limitLabel}: ${formatMegabytes(limitMegabytes)}`}</span>
        ) : null}
        <span>{`${t('labels.total')}: ${formatMegabytes(totalMegabytes)}`}</span>
      </div>
      <div
        aria-label={`${t('labels.gpuMemoryInUse')} ${formatMegabytes(usedMegabytes)} / ${formatMegabytes(totalMegabytes)}`}
        className="unified-memory-track"
        role="img"
      >
        {hasNumber(limitMegabytes) ? <div className="unified-memory-limit" style={{ width: `${limitPercent}%` }} /> : null}
        <div className="unified-memory-used" style={{ width: `${usedPercent}%` }} />
      </div>
    </div>
  )
}
