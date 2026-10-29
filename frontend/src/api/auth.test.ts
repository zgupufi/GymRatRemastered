import { afterEach, describe, expect, it, vi } from 'vitest'
import { login, register, WeightUnit } from './auth'
import { getUserProfile } from './users'

describe('auth api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts the credentials to the login endpoint and returns the token', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ accessToken: 'abc', expiresAt: '2999-01-01T00:00:00Z' }))
    vi.stubGlobal('fetch', fetchMock)

    const response = await login({ email: 'rafael@example.com', password: 'secret123' })

    expect(response.accessToken).toBe('abc')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/login')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'POST',
      body: JSON.stringify({ email: 'rafael@example.com', password: 'secret123' }),
    })
  })

  it('posts the registration to the register endpoint', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 201 }))
    vi.stubGlobal('fetch', fetchMock)

    await register({ email: 'a@b.co', password: 'secret123', firstName: 'A', lastName: null, weightUnit: WeightUnit.Pounds })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/register')
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toMatchObject({ weightUnit: 1, lastName: null })
  })

  it('loads the current user profile', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ firstName: 'Rafael' }))
    vi.stubGlobal('fetch', fetchMock)

    const profile = await getUserProfile()

    expect(profile.firstName).toBe('Rafael')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/users/me')
  })
})
