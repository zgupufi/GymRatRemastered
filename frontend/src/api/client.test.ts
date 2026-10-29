import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiClient, setAccessToken } from './client'

function mockFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('apiClient', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
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
