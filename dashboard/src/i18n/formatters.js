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

export function formatDurationHms(value) {
  if (value == null || Number.isNaN(value)) {
    return formatElapsedMs(value)
  }

  const totalSeconds = Math.max(0, Math.floor(value / 1000))
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  return `${formatNumber(hours)}h ${String(minutes).padStart(2, '0')}m ${String(seconds).padStart(2, '0')}s`
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

export function formatDateTimeWithSeconds(value) {
  if (!value) {
    return formatDateTime(value)
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return formatDateTime(null)
  }

  return new Intl.DateTimeFormat(locale(), {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit',
    timeZoneName: 'short',
  }).format(date)
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

export function hasNumber(value) {
  return value != null && typeof value === 'number' && !Number.isNaN(value)
}

export function formatOptional(value, unit = '', maximumFractionDigits = 1) {
  if (!hasNumber(value)) {
    return '—'
  }

  const formatted = formatNumber(value, { maximumFractionDigits })
  return unit ? `${formatted} ${unit}` : formatted
}

export function formatMegabytes(value) {
  return hasNumber(value) ? formatBytes(value * 1024 * 1024) : '—'
}

export function formatMemoryRange(usedMegabytes, totalMegabytes, sharedLabel = '') {
  if (!hasNumber(totalMegabytes) || totalMegabytes <= 0) {
    return '—'
  }

  const suffix = sharedLabel ? ` (${sharedLabel})` : ''
  return `${formatMegabytes(usedMegabytes || 0)} / ${formatMegabytes(totalMegabytes)}${suffix}`
}

export const thermalStateOrder = ['nominal', 'fair', 'serious', 'critical']

export function thermalStateTone(state) {
  if (state === 'serious') {
    return 'warning'
  }

  if (state === 'critical') {
    return 'danger'
  }

  return state ? 'success' : ''
}
