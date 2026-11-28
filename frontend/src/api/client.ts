import type { Schema } from './schema'

const BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? ''

export interface FieldError {
  property: string
  message: string
}

export interface ApiErrorBody {
  code: string
  message: string
  errors?: FieldError[]
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly fieldErrors: FieldError[]

  constructor(status: number, message: string, code?: string, fieldErrors: FieldError[] = []) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
  }
}

function isApiErrorBody(value: unknown): value is ApiErrorBody {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ApiErrorBody).code === 'string' &&
    typeof (value as ApiErrorBody).message === 'string'
  )
}

const INVALID_REFRESH_TOKEN_CODE = 'BLException_AuthService_InvalidRefreshToken'

export type RefreshOutcome = 'refreshed' | 'rejected' | 'unavailable'

let accessToken: string | null = null
let unauthorizedHandler: (() => void) | null = null
let refreshInFlight: Promise<RefreshOutcome> | null = null

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

export function setAccessToken(token: string | null): void {
  accessToken = token
}

async function requestRefresh(): Promise<RefreshOutcome> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: { Accept: 'application/json', 'Accept-Language': 'en' },
    })
  } catch {
    return 'unavailable'
  }

  const body: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    const rejected = response.status === 401 || (isApiErrorBody(body) && body.code === INVALID_REFRESH_TOKEN_CODE)
    return rejected ? 'rejected' : 'unavailable'
  }
  if (typeof body === 'object' && body !== null && typeof (body as Schema<'LoginResponse'>).accessToken === 'string') {
    accessToken = (body as Schema<'LoginResponse'>).accessToken
    return 'refreshed'
  }
  return 'unavailable'
}

export function refreshSession(): Promise<RefreshOutcome> {
  refreshInFlight ??= requestRefresh().finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

type ApiRequestInit = Omit<RequestInit, 'headers'> & { headers?: Record<string, string> }

async function send(path: string, init?: ApiRequestInit, retried = false): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      credentials: 'include',
      ...init,
      headers: {
        Accept: 'application/json',
        'Accept-Language': 'en',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...init?.headers,
      },
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError(0, 'Network request failed')
  }

  if (!response.ok) {
    if (response.status === 401 && accessToken) {
      const outcome = retried ? 'rejected' : await refreshSession()
      if (outcome === 'refreshed') {
        return send(path, init, true)
      }
      if (outcome === 'rejected') {
        unauthorizedHandler?.()
      }
    }
    const body: unknown = await response.json().catch(() => null)
    if (isApiErrorBody(body)) {
      throw new ApiError(response.status, body.message, body.code, body.errors ?? [])
    }
    throw new ApiError(response.status, `Request failed with status ${response.status}`)
  }

  return response
}

async function request<T>(path: string, init?: ApiRequestInit): Promise<T> {
  const response = await send(path, init)
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

function withJsonBody(method: string, body: unknown): ApiRequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }
}

export const apiClient = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),
  post: <T = void>(path: string, body: unknown, signal?: AbortSignal) =>
    request<T>(path, { ...withJsonBody('POST', body), signal }),
  put: (path: string, body: unknown, signal?: AbortSignal) =>
    request<void>(path, { ...withJsonBody('PUT', body), signal }),
  patch: (path: string, body: unknown) => request<void>(path, withJsonBody('PATCH', body)),
  delete: (path: string, body?: unknown) =>
    request<void>(path, body === undefined ? { method: 'DELETE' } : withJsonBody('DELETE', body)),
  download: async (path: string, signal?: AbortSignal) => (await send(path, { signal })).blob(),
}
