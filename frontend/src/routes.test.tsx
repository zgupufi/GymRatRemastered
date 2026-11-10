import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getExercise, searchExercises } from './api/exercises'
import { apiClient, setAccessToken } from './api/client'
import { login, WeightUnit } from './api/auth'
import { getUserProfile } from './api/users'
import { AuthProvider } from './auth/AuthProvider'
import { saveSession } from './auth/sessionStorage'
import { getSplit } from './api/split'
import { getWorkoutSummaries } from './api/workouts'
import { AppRoutes } from './routes'

vi.mock('./api/split')
vi.mock('./api/exercises')
vi.mock('./api/workouts')
vi.mock('./api/users')
vi.mock('./api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('./api/auth')>()),
  login: vi.fn(),
}))

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('AppRoutes', () => {
  beforeEach(() => {
    vi.mocked(searchExercises).mockResolvedValue({ items: [], page: 1, pageSize: 24, totalCount: 0 })
    vi.mocked(getExercise).mockResolvedValue({
      id: '0199e0a1-7c3e-7b5a-9d2f-3f6f0c1a2b3c',
      name: 'Bench Press',
      mainMuscle: 'Chest',
      subMuscles: null,
      equipment: 'Barbell',
      difficulty: 'Intermediate',
      description: null,
      youtubeLink: null,
    })
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 })
    vi.mocked(getSplit).mockResolvedValue([])
    vi.mocked(getUserProfile).mockResolvedValue({
      firstName: 'Rafael',
      lastName: null,
      email: 'r@example.com',
      weightUnit: WeightUnit.Kilograms,
      defaultRestTimeSeconds: 90,
    })
    saveSession({ accessToken: 'token', expiresAt: '2999-01-01T00:00:00Z' })
    window.scrollTo = vi.fn()
    Element.prototype.scrollIntoView = vi.fn()
  })

  afterEach(() => {
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
  })

  it.each([
    ['/exercises', 'Exercise library'],
    ['/exercises/0199e0a1-7c3e-7b5a-9d2f-3f6f0c1a2b3c', 'Bench Press'],
    ['/workouts', 'My workouts'],
    ['/split', 'My split'],
    ['/workout-execution', 'Rest day'],
    ['/progress', 'Progression'],
    ['/nowhere', 'Page not found'],
  ])('renders %s', async (path, heading) => {
    renderAt(path)

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })

  it.each([
    ['/login', 'Welcome back'],
    ['/register', 'Create account'],
  ])('renders %s for guests', async (path, heading) => {
    localStorage.clear()
    setAccessToken(null)
    renderAt(path)

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })

  it('mirrors the background glow on the register page only', async () => {
    localStorage.clear()
    setAccessToken(null)
    const { container, unmount } = renderAt('/register')
    await screen.findByRole('heading', { level: 1, name: 'Create account' })
    expect(container.querySelector('.app-shell--mirrored')).not.toBeNull()
    unmount()

    const login = renderAt('/login')
    await login.findByRole('heading', { level: 1, name: 'Welcome back' })
    expect(login.container.querySelector('.app-shell--mirrored')).toBeNull()
  })

  it('scrolls to the top on navigation', async () => {
    renderAt('/exercises')
    await screen.findByRole('heading', { level: 1, name: 'Exercise library' })

    expect(window.scrollTo).toHaveBeenCalledWith({ top: 0, left: 0, behavior: 'instant' })
  })

  it('goes back to the library from an exercise opened directly', async () => {
    renderAt('/exercises/0199e0a1-7c3e-7b5a-9d2f-3f6f0c1a2b3c')

    await userEvent.click(await screen.findByRole('button', { name: 'Back' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Exercise library' })).toBeInTheDocument()
  })

  it('shows the landing page to guests and the dashboard to members', async () => {
    localStorage.clear()
    setAccessToken(null)
    const guest = renderAt('/')
    expect(await guest.findByRole('heading', { level: 1, name: 'Log workouts. Get stronger. Stay motivated.' })).toBeInTheDocument()
    guest.unmount()

    saveSession({ accessToken: 'token', expiresAt: '2999-01-01T00:00:00Z' })
    renderAt('/')
    expect(await screen.findByRole('heading', { level: 1, name: 'Let’s set up your first week' })).toBeInTheDocument()
  })

  it('sends guests from a member page to the login page', async () => {
    localStorage.clear()
    setAccessToken(null)

    renderAt('/workouts')

    expect(await screen.findByRole('heading', { level: 1, name: 'Welcome back' })).toBeInTheDocument()
    expect(screen.queryByText(/session expired/i)).not.toBeInTheDocument()
  })

  it('returns to the requested page after logging in', async () => {
    localStorage.clear()
    setAccessToken(null)
    vi.mocked(login).mockResolvedValue({ accessToken: 'token', expiresAt: '2999-01-01T00:00:00Z' })
    renderAt('/split')

    await userEvent.type(await screen.findByLabelText('Email'), 'r@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'secret-pass')
    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'My split' })).toBeInTheDocument()
  })

  it('shows the member navigation on member pages', async () => {
    renderAt('/workouts')

    const main = within(await screen.findByRole('navigation', { name: 'Main' }))
    expect(screen.getByRole('navigation', { name: 'Sections' })).toBeInTheDocument()
    expect(main.getByRole('link', { name: 'Workouts' })).toHaveAttribute('aria-current', 'page')
  })

  it('sets the page title from the route', async () => {
    renderAt('/split')
    await screen.findByRole('heading', { level: 1, name: 'My split' })

    expect(document.title).toBe('Training split · GymRat')
  })

  it('returns to the login page with a notice when the server rejects the session', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status: 401 }))
    vi.mocked(getWorkoutSummaries).mockImplementation(() => apiClient.get('/api/workouts'))
    renderAt('/workouts')

    expect(await screen.findByText(/Your session expired/)).toBeInTheDocument()
    fetchSpy.mockRestore()
  })
})
