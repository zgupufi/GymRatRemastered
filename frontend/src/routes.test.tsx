import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getExercise, searchExercises } from './api/exercises'
import { AuthProvider } from './auth/AuthProvider'
import { getSplit } from './api/split'
import { getWorkoutSummaries } from './api/workouts'
import { AppRoutes } from './routes'

vi.mock('./api/split')
vi.mock('./api/exercises')
vi.mock('./api/workouts')

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
    window.scrollTo = vi.fn()
    Element.prototype.scrollIntoView = vi.fn()
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it.each([
    ['/', 'Log workouts. Get stronger. Stay motivated.'],
    ['/login', 'Welcome back'],
    ['/register', 'Create account'],
    ['/exercises', 'Exercise database'],
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

  it('mirrors the background glow on the register page only', async () => {
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
    await screen.findByRole('heading', { level: 1, name: 'Exercise database' })

    expect(window.scrollTo).toHaveBeenCalledWith({ top: 0, left: 0, behavior: 'instant' })
  })

  it('goes back to the home page from a page opened directly', async () => {
    renderAt('/progress')

    await userEvent.click(await screen.findByRole('button', { name: 'Back' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Log workouts. Get stronger. Stay motivated.' })).toBeInTheDocument()
  })
})
