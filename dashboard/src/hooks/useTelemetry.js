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

  async function refresh() {
    try {
      setError('')
      const [nextCapabilities, nextTelemetry] = await Promise.all([
        getCapabilities(),
        getTelemetry(),
      ])

      startTransition(() => {
        setCapabilities(nextCapabilities)
        setData(nextTelemetry)
      })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause))
    } finally {
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
