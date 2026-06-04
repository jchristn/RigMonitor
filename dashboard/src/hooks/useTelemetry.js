import { startTransition, useEffect, useRef, useState } from 'react'
import { getCapabilities, getTelemetry } from '../utils/api'

export function useTelemetry() {
  const [data, setData] = useState(null)
  const [capabilities, setCapabilities] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [intervalMs, setIntervalMs] = useState(5000)
  const timerRef = useRef(null)
  const refreshIdRef = useRef(0)

  function errorText(value) {
    return value instanceof Error ? value.message : String(value)
  }

  async function refresh() {
    const refreshId = refreshIdRef.current + 1
    refreshIdRef.current = refreshId

    const [capabilitiesResult, telemetryResult] = await Promise.allSettled([
      getCapabilities(),
      getTelemetry(),
    ])

    if (refreshId !== refreshIdRef.current) {
      return
    }

    const errors = []

    startTransition(() => {
      if (capabilitiesResult.status === 'fulfilled') {
        setCapabilities(capabilitiesResult.value)
      } else {
        errors.push(`capabilities: ${errorText(capabilitiesResult.reason)}`)
      }

      if (telemetryResult.status === 'fulfilled' && telemetryResult.value) {
        setData(telemetryResult.value)
      } else if (telemetryResult.status === 'rejected') {
        errors.push(`telemetry: ${errorText(telemetryResult.reason)}`)
      } else {
        errors.push('telemetry: empty response')
      }

      setError(errors.join('; '))
    })

    if (refreshId === refreshIdRef.current) {
      setLoading(false)
    }
  }

  useEffect(() => {
    void refresh()
  }, [])

  useEffect(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current)
    }

    if (autoRefresh) {
      timerRef.current = window.setInterval(() => {
        void refresh()
      }, intervalMs)
    }

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current)
      }
    }
  }, [autoRefresh, intervalMs])

  return {
    autoRefresh,
    capabilities,
    data,
    error,
    intervalMs,
    loading,
    refresh,
    setAutoRefresh,
    setIntervalMs,
  }
}
