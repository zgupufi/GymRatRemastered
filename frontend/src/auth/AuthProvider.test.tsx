import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, register, WeightUnit } from '../api/auth'
import { ApiError, apiClient, setAccessToken } from '../api/client'
import { getUserProfile, type UserProfile } from '../api/users'
import { useAuth } from './AuthContext'
import { AuthProvider } from './AuthProvider'
import { saveSession } from './sessionStorage'

vi.mock('../api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/auth')>()),
  login: vi.fn(),
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

  it('restores a stored session and loads the profile', async () => {
    saveSession({ accessToken: 'stored', expiresAt: FUTURE })
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json([]))
    vi.stubGlobal('fetch', fetchMock)

    renderProvider()
    expect(screen.getByText('status:loading')).toBeInTheDocument()

    expect(await screen.findByText('status:authenticated')).toBeInTheDocument()
    expect(screen.getByText('user:Rafael')).toBeInTheDocument()
    await apiClient.get('/api/things')
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get('Authorization')).toBe('Bearer stored')
  })

  it('ends the session when the stored token is rejected', async () => {
    saveSession({ accessToken: 'stale', expiresAt: FUTURE })
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError(401, 'Unauthorized'))

    renderProvider()

    expect(await screen.findByText('status:anonymous')).toBeInTheDocument()
    expect(localStorage.getItem('gymrat.session')).toBeNull()
  })

  it('keeps the stored session when the profile cannot be loaded for another reason', async () => {
    saveSession({ accessToken: 'stored', expiresAt: FUTURE })
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError(0, 'Network request failed'))

    renderProvider()

    expect(await screen.findByText('status:anonymous')).toBeInTheDocument()
    expect(localStorage.getItem('gymrat.session')).not.toBeNull()
  })

  it('ignores a profile response that arrives after unmounting', async () => {
    saveSession({ accessToken: 'stored', expiresAt: FUTURE })
    vi.mocked(getUserProfile).mockImplementation(
      (signal) =>
        new Promise((_resolve, reject) => {
          signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')))
        }),
    )

    const { unmount } = renderProvider()
    unmount()

    await act(async () => {})
    expect(localStorage.getItem('gymrat.session')).not.toBeNull()
  })

  it('logs in, loads the profile and stores the session', async () => {
    renderProvider()

    await userEvent.click(screen.getByRole('button', { name: 'login' }))

    expect(await screen.findByText('status:authenticated')).toBeInTheDocument()
    expect(login).toHaveBeenCalledWith({ email: 'rafael@example.com', password: 'secret' })
    expect(JSON.parse(localStorage.getItem('gymrat.session') ?? '{}')).toEqual({ accessToken: 'token-1', expiresAt: FUTURE })
  })

  it('stays anonymous and stores nothing when the profile fails after login', async () => {
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError(500, 'boom'))
    renderProvider()

    await userEvent.click(screen.getByRole('button', { name: 'login' }))

    await waitFor(() => expect(getUserProfile).toHaveBeenCalled())
    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(localStorage.getItem('gymrat.session')).toBeNull()
  })

  it('registers and then logs in with the same credentials', async () => {
    renderProvider()

    await userEvent.click(screen.getByRole('button', { name: 'register' }))

    expect(await screen.findByText('status:authenticated')).toBeInTheDocument()
    expect(register).toHaveBeenCalledWith(expect.objectContaining({ email: 'rafael@example.com', weightUnit: 0 }))
    expect(login).toHaveBeenCalledWith({ email: 'rafael@example.com', password: 'secret123' })
  })

  it('logs out and forgets the session', async () => {
    renderProvider()
    await userEvent.click(screen.getByRole('button', { name: 'login' }))
    await screen.findByText('status:authenticated')

    await userEvent.click(screen.getByRole('button', { name: 'logout' }))

    expect(screen.getByText('status:anonymous')).toBeInTheDocument()
    expect(screen.getByText('user:none')).toBeInTheDocument()
    expect(localStorage.getItem('gymrat.session')).toBeNull()
  })

  it('refuses to be used outside a provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    expect(() => render(<Probe />)).toThrow('useAuth must be used inside an AuthProvider')
  })
})
