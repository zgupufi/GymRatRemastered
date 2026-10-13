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

  it('throws a generic ApiError when the error body is not JSON', async () => {
    mockFetch(new Response('boom', { status: 500 }))

    const error: unknown = await apiClient.get('/api/things').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 500, message: 'Request failed with status 500' })
  })
})
