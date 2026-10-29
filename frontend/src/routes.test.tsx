import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getExercises } from './api/exercises'
import { AuthProvider } from './auth/AuthProvider'
import { AppRoutes } from './routes'

vi.mock('./api/exercises')

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
    vi.mocked(getExercises).mockResolvedValue([])
    window.scrollTo = vi.fn()
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it.each([
    ['/', 'Log workouts. Get stronger. Stay motivated.'],
    ['/login', 'Welcome back'],
    ['/register', 'Create account'],
    ['/exercises', 'Exercise database'],
    ['/exercises/0199e0a1-7c3e-7b5a-9d2f-3f6f0c1a2b3c', 'Exercise details'],
    ['/workouts', 'My workouts'],
    ['/split', 'My split'],
    ['/workout-execution', 'Workout'],
    ['/progress', 'Progression'],
    ['/nowhere', 'Page not found'],
  ])('renders %s', async (path, heading) => {
    renderAt(path)

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
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
