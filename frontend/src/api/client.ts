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

let accessToken: string | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
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
    const body: unknown = await response.json().catch(() => null)
    if (isApiErrorBody(body)) {
      throw new ApiError(response.status, body.message, body.code, body.errors ?? [])
    }
    throw new ApiError(response.status, `Request failed with status ${response.status}`)
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

function withJsonBody(method: string, body: unknown): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }
}

export const apiClient = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),
  post: <T = void>(path: string, body: unknown) => request<T>(path, withJsonBody('POST', body)),
  put: (path: string, body: unknown) => request<void>(path, withJsonBody('PUT', body)),
  delete: (path: string) => request<void>(path, { method: 'DELETE' }),
}
