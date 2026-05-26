import i18n from './index'

function locale() {
  return i18n.resolvedLanguage || 'en'
}

export function formatNumber(value, options = {}) {
  if (value == null || Number.isNaN(value)) {
    return '—'
  }

  return new Intl.NumberFormat(locale(), options).format(value)
}

export function formatPercent(value) {
  if (value == null || Number.isNaN(value)) {
    return '—'
  }

  return `${formatNumber(value, { maximumFractionDigits: 1 })}%`
}

export function formatBytes(value) {
  if (value == null || Number.isNaN(value)) {
    return '—'
  }

  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let size = value
  let index = 0

  while (size >= 1024 && index < units.length - 1) {
    size /= 1024
    index += 1
  }

  return `${formatNumber(size, { maximumFractionDigits: 1 })} ${units[index]}`
}

export function formatDuration(value) {
  if (value == null || Number.isNaN(value)) {
    return '—'
  }

  const totalSeconds = Math.floor(value / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)

  if (hours > 0) {
    return `${formatNumber(hours)}h ${formatNumber(minutes)}m`
  }

  return `${formatNumber(minutes)}m`
}

export function formatDateTime(value) {
  if (!value) {
    return '—'
  }

  return new Intl.DateTimeFormat(locale(), {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function formatElapsedMs(value) {
  if (value == null || Number.isNaN(value)) {
    return '—'
  }

  if (value < 1000) {
    return `${formatNumber(value, { maximumFractionDigits: 0 })} ms`
  }

  const seconds = value / 1000
  if (seconds < 60) {
    return `${formatNumber(seconds, { maximumFractionDigits: 1 })} s`
  }

  return formatDuration(value)
}
