import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiClient, refreshSession, setAccessToken, setUnauthorizedHandler } from './client'

function mockFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('apiClient', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
    setUnauthorizedHandler(null)
  })

  it('returns the parsed body of a successful GET', async () => {
    const fetchMock = mockFetch(Response.json([{ id: 1 }]))

    const result = await apiClient.get<{ id: number }[]>('/api/things')

    expect(result).toEqual([{ id: 1 }])
    expect(fetchMock.mock.calls[0][0]).toBe('/api/things')
  })

  it('sends the access token as a bearer header once it is set', async () => {
    const fetchMock = mockFetch(Response.json([]))
    setAccessToken('abc123')

    await apiClient.get('/api/things')

    expect(fetchMock.mock.calls[0][1]?.headers).toMatchObject({ Authorization: 'Bearer abc123' })
  })

  it('sends the language header together with the bearer token', async () => {
    const fetchMock = mockFetch(Response.json([]))
    setAccessToken('abc123')

    await apiClient.get('/api/things')

    expect(fetchMock.mock.calls[0][1]?.headers).toMatchObject({
      'Accept-Language': 'en',
      Authorization: 'Bearer abc123',
    })
  })

  it('asks for English messages on every request', async () => {
    const fetchMock = mockFetch(new Response(null, { status: 204 }))

    await apiClient.get('/api/things')
    await apiClient.post('/api/things', { name: 'x' })

    expect(fetchMock.mock.calls[0][1]?.headers).toMatchObject({ 'Accept-Language': 'en' })
    expect(fetchMock.mock.calls[1][1]?.headers).toMatchObject({
      'Accept-Language': 'en',
      'Content-Type': 'application/json',
    })
  })

  it('sends no authorization header without an access token', async () => {
    const fetchMock = mockFetch(Response.json([]))

    await apiClient.get('/api/things')

    expect(fetchMock.mock.calls[0][1]?.headers).not.toHaveProperty('Authorization')
  })

  it('resolves to undefined on 204 No Content', async () => {
    const fetchMock = mockFetch(new Response(null, { status: 204 }))

    const result = await apiClient.post('/api/things', { name: 'x' })

    expect(result).toBeUndefined()
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'POST', body: '{"name":"x"}' })
  })

  it('throws an ApiError carrying the backend error code', async () => {
    mockFetch(
      Response.json({ code: 'Exercises.NameRequired', message: 'Name is required.' }, { status: 400 }),
    )

    const error: unknown = await apiClient.post('/api/exercises', {}).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 400,
      code: 'Exercises.NameRequired',
      message: 'Name is required.',
    })
  })

  it('keeps the per-field errors of a validation failure', async () => {
    mockFetch(
      Response.json(
        {
          code: 'BLException_Validation_InvalidRequest',
          message: 'The request is not valid.',
          errors: [{ property: 'Email', message: 'Invalid email.' }],
        },
        { status: 400 },
      ),
    )

    const error: unknown = await apiClient.post('/api/auth/register', {}).catch((e: unknown) => e)

    expect(error).toMatchObject({ fieldErrors: [{ property: 'Email', message: 'Invalid email.' }] })
  })

  it('has no field errors when the backend sends none', async () => {
    mockFetch(Response.json({ code: 'X', message: 'Nope.' }, { status: 400 }))

    const error: unknown = await apiClient.post('/api/things', {}).catch((e: unknown) => e)

    expect(error).toMatchObject({ fieldErrors: [] })
  })

  it('resolves to undefined on 201 Created without a body', async () => {
    mockFetch(new Response(null, { status: 201 }))

    const result = await apiClient.post('/api/auth/register', {})

    expect(result).toBeUndefined()
  })

  it('returns the parsed body of a successful POST', async () => {
    mockFetch(Response.json({ accessToken: 'abc' }))

    const result = await apiClient.post<{ accessToken: string }>('/api/auth/login', { email: 'a' })

    expect(result).toEqual({ accessToken: 'abc' })
  })

  it('throws a generic ApiError when the error body is not JSON', async () => {
    mockFetch(new Response('boom', { status: 500 }))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 500, message: 'Request failed with status 500' })
  })

  it('sends a PUT request with a JSON body', async () => {
    const fetchMock = mockFetch(new Response(null, { status: 204 }))

    await apiClient.put('/api/things/1', { name: 'y' })

    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'PUT', body: '{"name":"y"}' })
  })

  it('sends a PATCH request with a JSON body', async () => {
    const fetchMock = mockFetch(new Response(null, { status: 204 }))

    await apiClient.patch('/api/things/1', { name: 'z' })

    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'PATCH', body: '{"name":"z"}' })
  })

  it('sends a DELETE request', async () => {
    const fetchMock = mockFetch(new Response(null, { status: 204 }))

    await apiClient.delete('/api/things/1')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/things/1')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'DELETE' })
  })

  it('throws an ApiError with status 0 when the network fails', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 0, message: 'Network request failed' })
  })

  it('rethrows an abort so callers can ignore it', async () => {
    const abort = new DOMException('aborted', 'AbortError')
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(abort))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toBe(abort)
  })
})

describe('silent refresh', () => {
  const INVALID = () =>
    Response.json({ code: 'BLException_AuthService_InvalidRefreshToken', message: 'The session has expired.' }, { status: 400 })
  const refreshed = (accessToken: string) => Response.json({ accessToken, expiresAt: '2999-01-01T00:00:00Z' })
  const unauthorized = () => new Response(null, { status: 401 })

  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
    setUnauthorizedHandler(null)
  })

  function stubFetch(handler: (url: string, init?: RequestInit) => Response | Promise<Response>) {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input, init) => Promise.resolve(handler(String(input), init)))
    vi.stubGlobal('fetch', fetchMock)
    return fetchMock
  }

  it('sends the cookies with every request', async () => {
    const fetchMock = stubFetch(() => Response.json([]))

    await apiClient.get('/api/things')

    expect(fetchMock.mock.calls[0][1]).toMatchObject({ credentials: 'include' })
  })

  it('refreshes once and retries the request with the new token when the access token expired', async () => {
    setAccessToken('expired')
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    const fetchMock = stubFetch((url, init) => {
      if (url === '/api/auth/refresh') {
        return refreshed('fresh')
      }
      return new Headers(init?.headers).get('Authorization') === 'Bearer fresh' ? Response.json({ id: 7 }) : unauthorized()
    })

    const result = await apiClient.get<{ id: number }>('/api/things')

    expect(result).toEqual({ id: 7 })
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual(['/api/things', '/api/auth/refresh', '/api/things'])
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: 'POST', credentials: 'include' })
    expect(handler).not.toHaveBeenCalled()
  })

  it('retries a save with the same body', async () => {
    setAccessToken('expired')
    const fetchMock = stubFetch((url, init) => {
      if (url === '/api/auth/refresh') {
        return refreshed('fresh')
      }
      return new Headers(init?.headers).get('Authorization') === 'Bearer fresh' ? new Response(null, { status: 204 }) : unauthorized()
    })

    await apiClient.put('/api/sessions/1/sets', { reps: 8 })

    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: 'PUT', body: '{"reps":8}' })
  })

  it('ends the session when the refresh cookie is rejected', async () => {
    setAccessToken('expired')
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    stubFetch((url) => (url === '/api/auth/refresh' ? INVALID() : unauthorized()))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toMatchObject({ status: 401 })
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('ends the session when a refreshed token is refused again', async () => {
    setAccessToken('expired')
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    const fetchMock = stubFetch((url) => (url === '/api/auth/refresh' ? refreshed('fresh') : unauthorized()))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toMatchObject({ status: 401 })
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('keeps the session when the refresh cannot be completed', async () => {
    setAccessToken('expired')
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    stubFetch((url) => (url === '/api/auth/refresh' ? new Response('boom', { status: 503 }) : unauthorized()))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toMatchObject({ status: 401 })
    expect(handler).not.toHaveBeenCalled()
  })

  it('does not try to refresh a 401 that carried no access token', async () => {
    const fetchMock = stubFetch(() => unauthorized())

    await apiClient.get('/api/things').catch(() => undefined)

    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('shares one refresh between requests that expire together', async () => {
    setAccessToken('expired')
    const fetchMock = stubFetch((url, init) => {
      if (url === '/api/auth/refresh') {
        return refreshed('fresh')
      }
      return new Headers(init?.headers).get('Authorization') === 'Bearer fresh' ? Response.json({ ok: true }) : unauthorized()
    })

    await Promise.all([apiClient.get('/api/a'), apiClient.get('/api/b'), apiClient.get('/api/c')])

    expect(fetchMock.mock.calls.filter(([url]) => url === '/api/auth/refresh')).toHaveLength(1)
  })

  describe('refreshSession', () => {
    it('stores the new access token', async () => {
      stubFetch(() => refreshed('fresh'))

      const outcome = await refreshSession()

      expect(outcome).toBe('refreshed')
      const fetchMock = stubFetch(() => Response.json([]))
      await apiClient.get('/api/things')
      expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get('Authorization')).toBe('Bearer fresh')
    })

    it('reports a rejected session for an invalid refresh token', async () => {
      stubFetch(() => INVALID())

      expect(await refreshSession()).toBe('rejected')
    })

    it('reports a rejected session for a 401', async () => {
      stubFetch(() => unauthorized())

      expect(await refreshSession()).toBe('rejected')
    })

    it('reports unavailable when the server is rate limiting', async () => {
      stubFetch(() => Response.json({ code: 'BLException_AuthService_TooManyAttempts', message: 'Slow down.' }, { status: 400 }))

      expect(await refreshSession()).toBe('unavailable')
    })

    it('reports unavailable when the network fails', async () => {
      vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')))

      expect(await refreshSession()).toBe('unavailable')
    })

    it('reports unavailable when the answer has no access token', async () => {
      stubFetch(() => Response.json({ nope: true }))

      expect(await refreshSession()).toBe('unavailable')
    })

    it('reports unavailable when the answer is empty', async () => {
      stubFetch(() => new Response(null, { status: 200 }))

      expect(await refreshSession()).toBe('unavailable')
    })

    it('can run again after a refresh finished', async () => {
      const fetchMock = stubFetch(() => refreshed('fresh'))

      await refreshSession()
      await refreshSession()

      expect(fetchMock).toHaveBeenCalledTimes(2)
    })
  })
})
