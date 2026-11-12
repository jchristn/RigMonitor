import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { GpuVendorSections } from '../components/gpu/GpuVendorSections'
import { amdSection, appleSection, nvidiaSection, qualcommSection, snapshot } from './fixtures'

function section(title) {
  return screen.getByRole('heading', { name: title }).closest('section')
}

describe('GpuVendorSections', () => {
  it('renders NVIDIA, AMD, Apple, and Qualcomm sections in a fixed order', () => {
    render(<GpuVendorSections data={snapshot({ nvidia: nvidiaSection, amd: amdSection, apple: appleSection, qualcomm: qualcommSection })} />)
    const headings = screen.getAllByRole('heading', { level: 2 }).map((node) => node.textContent)
    expect(headings).toEqual(['NVIDIA GPUs', 'AMD GPUs', 'Apple GPU', 'Qualcomm GPUs'])
  })

  it('renders NVIDIA device values and the source', () => {
    render(<GpuVendorSections data={snapshot({ nvidia: nvidiaSection })} />)
    const card = section('NVIDIA GPUs')
    expect(within(card).getByText('Source: DCGM exporter')).toBeTruthy()
    expect(within(card).getByText('NVIDIA L40S')).toBeTruthy()
    expect(within(card).getByText('61 C')).toBeTruthy()
    expect(within(card).getByText('250 W')).toBeTruthy()
  })

  it('shows AMD gap-fill, library version, partition, and edge/hotspot temperatures', () => {
    render(<GpuVendorSections data={snapshot({ amd: amdSection })} />)
    const card = section('AMD GPUs')
    expect(within(card).getByText('Gap-fill: amdgpu sysfs')).toBeTruthy()
    expect(within(card).getByText('Library: 6.4.0')).toBeTruthy()
    expect(within(card).getByText('AMD Instinct MI300X (partition 0)')).toBeTruthy()
    expect(within(card).getByText('70 C / 75 C')).toBeTruthy()
  })

  it('shows Apple chip details, thermal state, and the unified memory bar without a fake temperature', () => {
    render(<GpuVendorSections data={snapshot({ apple: appleSection })} />)
    const card = section('Apple GPU')
    expect(within(card).getByText('40-core GPU')).toBeTruthy()
    expect(within(card).getByText('Metal family: apple10')).toBeTruthy()
    expect(within(card).getByText('Thermal state: Serious').className).toContain('warning')
    expect(within(card).getByTestId('unified-memory-bar')).toBeTruthy()
    expect(within(card).queryByText('0 C')).toBeNull()
    expect(within(card).getAllByText('—').length).toBeGreaterThan(0)
  })

  it('labels Qualcomm utilization that only covers visible processes', () => {
    render(<GpuVendorSections data={snapshot({ qualcomm: qualcommSection })} />)
    const card = section('Qualcomm GPUs')
    expect(within(card).getByText('50% (own processes only)')).toBeTruthy()
    expect(within(card).getByText('680 MHz / 1,250 MHz')).toBeTruthy()
  })

  it('expands a device row to show vendor-specific details', () => {
    render(<GpuVendorSections data={snapshot({ nvidia: nvidiaSection })} />)
    const card = section('NVIDIA GPUs')
    const toggle = within(card).getByRole('button', { name: 'Details' })
    expect(within(card).queryByText('XID errors')).toBeNull()
    fireEvent.click(toggle)
    expect(within(card).getByText('XID errors')).toBeTruthy()
    expect(within(card).getByText('GPU-1')).toBeTruthy()
    expect(toggle.getAttribute('aria-expanded')).toBe('true')
  })

  it('shows placeholders with setup guides for vendors that are not present', () => {
    render(<GpuVendorSections data={snapshot({ apple: appleSection })} />)
    const card = section('AMD GPUs')
    expect(within(card).getAllByText('unsupported message').length).toBe(2)
    expect(within(card).getByRole('link', { name: 'Setup guide' }).getAttribute('href')).toContain('INSTALLING_AMD_GPU_TELEMETRY.md')
  })

  it('hides unsupported vendors when asked', () => {
    render(<GpuVendorSections data={snapshot({ apple: appleSection })} hideUnsupported />)
    const headings = screen.getAllByRole('heading', { level: 2 }).map((node) => node.textContent)
    expect(headings).toEqual(['Apple GPU'])
  })
})
