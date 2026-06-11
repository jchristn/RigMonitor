async function getJson(path) {
  const response = await fetch(path, {
    headers: {
      Accept: 'application/json',
    },
  })

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`)
  }

  return response.json()
}

async function sendJson(method, path, body = null) {
  const request = {
    method,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
  }

  if (body !== null) {
    request.body = JSON.stringify(body)
  }

  const response = await fetch(path, request)

  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`
    try {
      const payload = await response.json()
      message = payload.error || payload.Error || message
    } catch {
      message = `${response.status} ${response.statusText}`
    }

    throw new Error(message)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

export function getTelemetry() {
  return getJson('/v1/telemetry')
}

export function getCapabilities() {
  return getJson('/v1/capabilities')
}

export function getOpenApi() {
  return getJson('/openapi.json')
}

export function enumerateTelemetryHistory(query) {
  return sendJson('POST', '/v1/telemetry/history/enumerate', query)
}

export function searchTelemetryHistory(filter) {
  return sendJson('POST', '/v1/telemetry/history/search', filter)
}

export function getTelemetrySample(sampleId) {
  return getJson(`/v1/telemetry/history/${encodeURIComponent(sampleId)}`)
}

export function rollupTelemetryHistory(request) {
  return sendJson('POST', '/v1/telemetry/history/rollups', request)
}

export function getTelemetryHistoryStatus() {
  return getJson('/v1/telemetry/history/status')
}

export function deleteTelemetrySample(sampleId) {
  return sendJson('DELETE', `/v1/telemetry/history/${encodeURIComponent(sampleId)}`)
}

export function deleteTelemetryHistory(filter) {
  return sendJson('DELETE', '/v1/telemetry/history', filter)
}
