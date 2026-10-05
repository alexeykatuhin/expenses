const appBaseUrl = import.meta.env.BASE_URL.replace(/\/$/, '')
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || `${appBaseUrl}/api/v1`

const authHandlers = {
  getAccessToken: () => null,
  refreshToken: () => Promise.resolve(false),
  onUnauthorized: () => {},
}

export class ApiError extends Error {
  constructor(message, { status, code, details, response } = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.details = details || []
    this.response = response
  }
}

export function configureAuthHandlers(handlers) {
  Object.assign(authHandlers, handlers)
}

export function buildQuery(params = {}) {
  const searchParams = new URLSearchParams()

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return
    }

    searchParams.set(key, String(value))
  })

  const query = searchParams.toString()
  return query ? `?${query}` : ''
}

function buildUrl(path, query) {
  const normalizedBaseUrl = API_BASE_URL.replace(/\/$/, '')
  const normalizedPath = path.startsWith('/') ? path : `/${path}`

  return `${normalizedBaseUrl}${normalizedPath}${buildQuery(query)}`
}

async function parseResponse(response) {
  if (response.status === 204) {
    return null
  }

  const text = await response.text()
  if (!text) {
    return null
  }

  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

function createApiError(response, data) {
  const apiError = data?.error

  if (apiError) {
    return new ApiError(apiError.message || 'API request failed', {
      status: response.status,
      code: apiError.code,
      details: apiError.details,
      response: data,
    })
  }

  return new ApiError(response.statusText || 'API request failed', {
    status: response.status,
    response: data,
  })
}

export async function httpRequest(path, options = {}) {
  const {
    method = 'GET',
    body,
    headers = {},
    auth = true,
    retryOnUnauthorized = true,
    query,
    signal,
  } = options

  const requestHeaders = new Headers(headers)

  if (body !== undefined && !(body instanceof FormData)) {
    requestHeaders.set('Content-Type', 'application/json')
  }

  if (auth) {
    const accessToken = authHandlers.getAccessToken()
    if (accessToken) {
      requestHeaders.set('Authorization', `Bearer ${accessToken}`)
    }
  }

  const response = await fetch(buildUrl(path, query), {
    method,
    headers: requestHeaders,
    body: body === undefined || body instanceof FormData ? body : JSON.stringify(body),
    signal,
  })

  const data = await parseResponse(response)

  if (response.status === 401 && auth && retryOnUnauthorized) {
    const refreshed = await authHandlers.refreshToken()

    if (refreshed) {
      return httpRequest(path, {
        ...options,
        retryOnUnauthorized: false,
      })
    }

    authHandlers.onUnauthorized()
  }

  if (!response.ok) {
    throw createApiError(response, data)
  }

  return data
}

export const http = {
  get: (path, options) => httpRequest(path, { ...options, method: 'GET' }),
  post: (path, body, options) => httpRequest(path, { ...options, method: 'POST', body }),
  patch: (path, body, options) => httpRequest(path, { ...options, method: 'PATCH', body }),
  delete: (path, options) => httpRequest(path, { ...options, method: 'DELETE' }),
}
