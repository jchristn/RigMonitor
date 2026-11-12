import { describe, expect, it } from 'vitest'
import { gpuMemorySummary, gpuPowerSummary, gpuUtilizationSummary } from '../views/HomeView'
import { appleSection, nvidiaSection } from './fixtures'

const mixed = { devices: [...nvidiaSection.devices, ...appleSection.devices] }

describe('overview GPU summaries', () => {
  it('sums memory across vendors and marks shared memory', () => {
    expect(gpuMemorySummary(mixed, 'shared')).toContain('shared')
    expect(gpuMemorySummary({ devices: [] }, 'shared')).toBe('—')
  })

  it('averages utilization across devices', () => {
    expect(gpuUtilizationSummary(mixed)).toBeCloseTo((73 + 14) / 2)
    expect(gpuUtilizationSummary(null)).toBeNull()
  })

  it('sums only reported power', () => {
    expect(gpuPowerSummary(mixed)).toBeCloseTo(250.3)
    expect(gpuPowerSummary({ devices: [{ metrics: {} }] })).toBeNull()
  })
})
