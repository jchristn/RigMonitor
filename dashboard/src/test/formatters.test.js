import { describe, expect, it } from 'vitest'
import { formatMegabytes, formatMemoryRange, formatOptional, hasNumber, thermalStateTone } from '../i18n/formatters'

describe('GPU formatters', () => {
  it('renders missing values as an em dash instead of zero', () => {
    expect(formatOptional(null, 'C')).toBe('—')
    expect(formatOptional(undefined, 'W')).toBe('—')
    expect(formatOptional(Number.NaN, 'W')).toBe('—')
    expect(formatOptional(0, 'W')).toBe('0 W')
    expect(formatOptional(61.25, 'C')).toBe('61.3 C')
  })

  it('formats megabytes and memory ranges', () => {
    expect(formatMegabytes(1024)).toBe('1 GB')
    expect(formatMegabytes(null)).toBe('—')
    expect(formatMemoryRange(1024, 2048)).toBe('1 GB / 2 GB')
    expect(formatMemoryRange(1024, 2048, 'shared')).toBe('1 GB / 2 GB (shared)')
    expect(formatMemoryRange(1024, 0)).toBe('—')
  })

  it('maps thermal states to tones', () => {
    expect(thermalStateTone('nominal')).toBe('success')
    expect(thermalStateTone('fair')).toBe('success')
    expect(thermalStateTone('serious')).toBe('warning')
    expect(thermalStateTone('critical')).toBe('danger')
    expect(thermalStateTone(null)).toBe('')
  })

  it('detects numbers', () => {
    expect(hasNumber(0)).toBe(true)
    expect(hasNumber('1')).toBe(false)
    expect(hasNumber(null)).toBe(false)
  })
})
