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

export function getTelemetry() {
  return getJson('/v1/telemetry')
}

export function getCapabilities() {
  return getJson('/v1/capabilities')
}

export function getOpenApi() {
  return getJson('/openapi.json')
}
