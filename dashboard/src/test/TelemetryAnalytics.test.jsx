import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import {
  chartSeries,
  descriptorAvailable,
  metricDescriptors,
  RollupChart,
  vendorsWithData,
  weightedAverage,
  weightedVendorAverage,
} from '../views/TelemetryAnalyticsView'

const buckets = [
  {
    bucketStartUtc: '2026-11-12T06:00:00Z',
    bucketEndUtc: '2026-11-12T07:00:00Z',
    sampleCount: 2,
    averageGpuUtilizationPercent: 50,
    averageGpuTemperatureCelsius: null,
    gpuVendors: {
      nvidia: { sampleCount: 2, averageUtilizationPercent: 30, averagePowerUsageWatts: 300 },
      amd: { sampleCount: 2, averageUtilizationPercent: 70, averageHotspotTemperatureCelsius: 80 },
    },
  },
  {
    bucketStartUtc: '2026-11-12T07:00:00Z',
    bucketEndUtc: '2026-11-12T08:00:00Z',
    sampleCount: 1,
    averageGpuUtilizationPercent: 20,
    averageGpuTemperatureCelsius: 60,
    gpuVendors: {
      nvidia: { sampleCount: 1, averageUtilizationPercent: 20 },
    },
  },
]

const descriptor = (key) => metricDescriptors.find((item) => item.key === key)

describe('analytics GPU series', () => {
  it('builds an all-GPU series plus one series per vendor with data', () => {
    expect(chartSeries(descriptor('gpu'), buckets).map((series) => series.key)).toEqual(['all', 'nvidia', 'amd'])
    expect(chartSeries(descriptor('cpu'), buckets).map((series) => series.key)).toEqual(['all'])
    expect(chartSeries(descriptor('amdHotspot'), buckets).map((series) => series.key)).toEqual(['amd'])
  })

  it('hides vendor-only charts when the range has no data for that vendor', () => {
    expect(descriptorAvailable(descriptor('amdHotspot'), buckets)).toBe(true)
    expect(descriptorAvailable(descriptor('appleThermalState'), buckets)).toBe(false)
    expect(vendorsWithData(buckets, 'averagePowerUsageWatts')).toEqual(['nvidia'])
  })

  it('skips null buckets in weighted averages', () => {
    expect(weightedAverage(buckets, 'averageGpuTemperatureCelsius')).toBe(60)
    expect(weightedVendorAverage(buckets, 'nvidia', 'averageUtilizationPercent')).toBeCloseTo((30 * 2 + 20) / 3)
    expect(weightedVendorAverage(buckets, 'apple', 'averageUtilizationPercent')).toBeNull()
  })

  it('renders a legend with toggles for multi-series GPU charts', () => {
    render(
      <RollupChart
        buckets={buckets}
        descriptor={descriptor('gpu')}
        emptyLabel="empty"
        label="chart"
        metricLabel="GPU utilization"
        sampleLabel="Samples"
        timeLabel="Collected"
      />
    )

    const nvidia = screen.getByRole('button', { name: 'NVIDIA' })
    expect(screen.getByRole('button', { name: 'All GPUs' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'AMD' })).toBeTruthy()
    expect(document.querySelectorAll('.chart-series').length).toBe(3)
    fireEvent.click(nvidia)
    expect(nvidia.getAttribute('aria-pressed')).toBe('false')
    expect(document.querySelectorAll('.chart-series').length).toBe(2)
  })

  it('draws gaps instead of zeros for missing vendor values', () => {
    render(
      <RollupChart
        buckets={buckets}
        descriptor={descriptor('gpuPower')}
        emptyLabel="empty"
        label="chart"
        metricLabel="GPU power"
        sampleLabel="Samples"
        timeLabel="Collected"
      />
    )

    const nvidiaSeries = document.querySelector('.chart-series.series-nvidia')
    expect(nvidiaSeries.querySelectorAll('circle').length).toBe(1)
  })
})
