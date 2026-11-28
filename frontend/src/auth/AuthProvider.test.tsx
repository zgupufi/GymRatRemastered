import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, logout, register, WeightUnit } from '../api/auth'
import { ApiError, apiClient, setAccessToken } from '../api/client'
import { getUserProfile, type UserProfile } from '../api/users'
import { LoginAfterRegisterError, useAuth } from './AuthContext'
import { AuthProvider } from './AuthProvider'
import { hasSessionHint, markSessionActive } from './sessionHint'

vi.mock('../api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/auth')>()),
  login: vi.fn(),
  logout: vi.fn(),
  register: vi.fn(),
}))
vi.mock('../api/users')

const PROFILE: UserProfile = {
  firstName: 'Rafael',
  lastName: null,
  email: 'rafael@example.com',
  weightUnit: WeightUnit.Kilograms,
  defaultRestTimeSeconds: 90,
}
const FUTURE = '2999-01-01T00:00:00Z'
const HINT_KEY = 'gymrat.signedIn'

function stubRefresh(response: () => Response | Promise<Response>) {
  const fetchMock = vi.fn<typeof fetch>().mockImplementation(() => Promise.resolve(response()))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function refreshed(accessToken = 'refreshed-token') {
  return Response.json({ accessToken, expiresAt: FUTURE })
}

function invalidRefreshToken() {
  return Response.json({ code: 'BLException_AuthService_InvalidRefreshToken', message: 'The session has expired.' }, { status: 400 })
}

function Probe() {
  const auth = useAuth()
  return (
    <>
      <p>status:{auth.status}</p>
      <p>user:{auth.user?.firstName ?? 'none'}</p>
      <button onClick={() => auth.login('rafael@example.com', 'secret').catch(() => undefined)}>login</button>
      <button onClick={() => auth.register({ email: 'rafael@example.com', password: 'secret123', firstName: 'Rafael', lastName: null, weightUnit: WeightUnit.Kilograms }).catch(() => undefined)}>register</button>
      <button onClick={auth.logout}>logout</button>
    </>
  )
}

function renderProvider() {
  return render(
    <AuthProvider>
      <Probe />
    </AuthProvider>,
  )
}

describe('AuthProvider', () => {
  beforeEach(() => {
    vi.mocked(getUserProfile).mockResolvedValue(PROFILE)
    vi.mocked(login).mockResolvedValue({ accessToken: 'token-1', expiresAt: FUTURE })
    vi.mocked(register).mockResolvedValue(undefined)
    vi.mocked(logout).mockResolvedValue(undefined)
  })

  afterEach(() => {
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
    vi.unstubAllGlobals()
  })

  it('starts anonymous without a stored session', () => {
    renderProvider()

    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(getUserProfile).not.toHaveBeenCalled()
  })

  it('restores the session from the refresh cookie and loads the profile', async () => {
    markSessionActive()
    const fetchMock = stubRefresh(() => refreshed())

    renderProvider()
    expect(screen.getByText('status:loading')).toBeInTheDocument()

    expect(await screen.findByText('status:authenticated')).toBeInTheDocument()
    expect(screen.getByText('user:Rafael')).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/refresh')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'POST', credentials: 'include' })
    await apiClient.get('/api/things')
    expect(new Headers(fetchMock.mock.calls[1][1]?.headers).get('Authorization')).toBe('Bearer refreshed-token')
  })

  it('shares one refresh between two mounts that start together', async () => {
    markSessionActive()
    const fetchMock = stubRefresh(() => refreshed())

    renderProvider()
    renderProvider()

    await waitFor(() => expect(screen.getAllByText('status:authenticated')).toHaveLength(2))
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('does not call the server when no session was ever started', () => {
    const fetchMock = stubRefresh(() => refreshed())

    renderProvider()

    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('ends the session when the refresh cookie is rejected', async () => {
    markSessionActive()
    stubRefresh(() => invalidRefreshToken())

    renderProvider()

    expect(await screen.findByText('status:anonymous')).toBeInTheDocument()
    expect(hasSessionHint()).toBe(false)
    expect(getUserProfile).not.toHaveBeenCalled()
  })

  it('keeps the session hint when the server cannot be reached to refresh', async () => {
    markSessionActive()
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')))

    renderProvider()

    expect(await screen.findByText('status:anonymous')).toBeInTheDocument()
    expect(hasSessionHint()).toBe(true)
    expect(getUserProfile).not.toHaveBeenCalled()
  })

  it('ends the session when the profile is refused after a refresh', async () => {
    markSessionActive()
    stubRefresh(() => refreshed())
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError(401, 'Unauthorized'))

    renderProvider()

    expect(await screen.findByText('status:anonymous')).toBeInTheDocument()
    expect(hasSessionHint()).toBe(false)
  })

  it('keeps the session hint when the profile cannot be loaded for another reason', async () => {
    markSessionActive()
    stubRefresh(() => refreshed())
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError(0, 'Network request failed'))

    renderProvider()

    expect(await screen.findByText('status:anonymous')).toBeInTheDocument()
    expect(hasSessionHint()).toBe(true)
  })

  it('ignores a refresh that finishes after unmounting', async () => {
    markSessionActive()
    let finish: (response: Response) => void = () => undefined
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockReturnValue(new Promise<Response>((resolve) => (finish = resolve))))

    const { unmount } = renderProvider()
    unmount()
    await act(async () => finish(refreshed()))

    expect(getUserProfile).not.toHaveBeenCalled()
    expect(hasSessionHint()).toBe(true)
  })

  it('ignores a profile response that arrives after unmounting', async () => {
    markSessionActive()
    stubRefresh(() => refreshed())
    vi.mocked(getUserProfile).mockImplementation(
      (signal) =>
        new Promise((_resolve, reject) => {
          signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')))
        }),
    )

    const { unmount } = renderProvider()
    await waitFor(() => expect(getUserProfile).toHaveBeenCalled())
    unmount()

    await act(async () => {})
    expect(hasSessionHint()).toBe(true)
  })

  it('logs in, loads the profile and remembers only that a session exists', async () => {
    renderProvider()

    await userEvent.click(screen.getByRole('button', { name: 'login' }))

    expect(await screen.findByText('status:authenticated')).toBeInTheDocument()
    expect(login).toHaveBeenCalledWith({ email: 'rafael@example.com', password: 'secret' })
    expect(localStorage.getItem(HINT_KEY)).toBe('1')
    expect(JSON.stringify(Object.entries(localStorage))).not.toContain('token-1')
  })

  it('stays anonymous and stores nothing when the profile fails after login', async () => {
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError(500, 'boom'))
    renderProvider()

    await userEvent.click(screen.getByRole('button', { name: 'login' }))

    await waitFor(() => expect(getUserProfile).toHaveBeenCalled())
    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(localStorage.getItem(HINT_KEY)).toBeNull()
    expect(logout).toHaveBeenCalledTimes(1)
  })

  it('registers and then logs in with the same credentials', async () => {
    renderProvider()

    await userEvent.click(screen.getByRole('button', { name: 'register' }))

    expect(await screen.findByText('status:authenticated')).toBeInTheDocument()
    expect(register).toHaveBeenCalledWith(expect.objectContaining({ email: 'rafael@example.com', weightUnit: 0 }))
    expect(login).toHaveBeenCalledWith({ email: 'rafael@example.com', password: 'secret123' })
  })

  it('reports a created account whose follow-up login failed', async () => {
    vi.mocked(login).mockRejectedValue(new ApiError(0, 'Network request failed'))
    let failure: unknown
    function Registrar() {
      const auth = useAuth()
      return (
        <button
          onClick={() =>
            auth
              .register({ email: 'rafael@example.com', password: 'secret123', firstName: 'Rafael', lastName: null, weightUnit: WeightUnit.Kilograms })
              .catch((error: unknown) => {
                failure = error
              })
          }
        >
          go
        </button>
      )
    }
    render(
      <AuthProvider>
        <Registrar />
        <Probe />
      </AuthProvider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'go' }))

    await waitFor(() => expect(failure).toBeInstanceOf(LoginAfterRegisterError))
    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
  })

  it('logs out, revokes the refresh token and forgets the session', async () => {
    renderProvider()
    await userEvent.click(screen.getByRole('button', { name: 'login' }))
    await screen.findByText('status:authenticated')

    await userEvent.click(screen.getByRole('button', { name: 'logout' }))

    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(screen.getByText('user:none')).toBeInTheDocument()
    expect(localStorage.getItem(HINT_KEY)).toBeNull()
    expect(logout).toHaveBeenCalledTimes(1)
  })

  it('logs out locally even when the server cannot be reached', async () => {
    vi.mocked(logout).mockRejectedValue(new ApiError(0, 'Network request failed'))
    renderProvider()
    await userEvent.click(screen.getByRole('button', { name: 'login' }))
    await screen.findByText('status:authenticated')

    await userEvent.click(screen.getByRole('button', { name: 'logout' }))

    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(localStorage.getItem(HINT_KEY)).toBeNull()
  })

  it('refuses to be used outside a provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    expect(() => render(<Probe />)).toThrow('useAuth must be used inside an AuthProvider')
  })
})
