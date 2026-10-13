const BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? ''

/** Error body returned by the backend when a business rule fails (HTTP 400). */
export interface ApiErrorBody {
  code: string
  message: string
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(status: number, message: string, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: { Accept: 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null)
    if (isApiErrorBody(body)) {
      throw new ApiError(response.status, body.message, body.code)
    }
    throw new ApiError(response.status, `Request failed with status ${response.status}`)
  }

  // Creates, updates and deletes answer 204 No Content.
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

function withJsonBody(method: string, body: unknown): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }
}

/** The single typed entry point for backend calls; components never call fetch directly. */
export const apiClient = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),
  post: (path: string, body: unknown) => request<void>(path, withJsonBody('POST', body)),
  put: (path: string, body: unknown) => request<void>(path, withJsonBody('PUT', body)),
  delete: (path: string) => request<void>(path, { method: 'DELETE' }),
}
