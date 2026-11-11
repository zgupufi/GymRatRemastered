import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, WeightUnit } from '../../api/auth'
import { ApiError, setAccessToken } from '../../api/client'
import { getLastSession } from '../../api/sessions'
import { getSplit } from '../../api/split'
import { getUserProfile, updateUserSettings, type UserProfile } from '../../api/users'
import { getWorkout, getWorkoutSummaries, type Workout, type WorkoutSummary } from '../../api/workouts'
import { AuthProvider } from '../../auth/AuthProvider'
import { saveSession } from '../../auth/sessionStorage'
import { AppNav } from '../../components/layout/AppNav'
import { ToastProvider } from '../../components/ui/Toast'
import { HomePage } from '../HomePage'
import { SettingsModal } from './SettingsModal'

vi.mock('../../api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/auth')>()),
  login: vi.fn(),
}))
vi.mock('../../api/users')
vi.mock('../../api/split')
vi.mock('../../api/sessions')
vi.mock('../../api/workouts')

const push: WorkoutSummary = { id: 'w1', name: 'Push day', exerciseCount: 2, mainMuscles: ['Chest'] }
const pull: WorkoutSummary = { id: 'w2', name: 'Pull day', exerciseCount: 1, mainMuscles: ['Back'] }

function exercise(id: string, name: string) {
  return { id, name, mainMuscle: 'Chest', subMuscles: null, equipment: null, difficulty: null, description: null, youtubeLink: null }
}

const pushDetails: Workout = {
  id: 'w1',
  name: 'Push day',
  exercises: [
    { order: 2, targetSets: 1, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: exercise('e2', 'Cable Fly') },
    { order: 1, targetSets: 3, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: exercise('e1', 'Bench Press') },
  ],
}

function todayNumber() {
  const jsDay = new Date().getDay()
  return jsDay === 0 ? 7 : jsDay
}

function splitWith(assignments: Record<number, WorkoutSummary>) {
  return [1, 2, 3, 4, 5, 6, 7].map((day) => ({ day, workout: assignments[day] ?? null }))
}

function otherDay() {
  return (todayNumber() % 7) + 1
}

function mockWorkouts(total: number) {
  vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: total > 0 ? [push] : [], page: 1, pageSize: 1, totalCount: total })
}

const PROFILE: UserProfile = {
  firstName: 'Rafael',
  lastName: 'Foica',
  email: 'rafael@example.com',
  weightUnit: WeightUnit.Kilograms,
  defaultRestTimeSeconds: 90,
}

function renderHome() {
  saveSession({ accessToken: 'token', expiresAt: '2999-01-01T00:00:00Z' })
  return render(
    <MemoryRouter initialEntries={['/']}>
      <AuthProvider>
        <ToastProvider>
          <Routes>
            <Route
              element={
                <>
                  <AppNav />
                  <Outlet />
                </>
              }
            >
              <Route index element={<HomePage />} />
              <Route path="workouts" element={<h1>Workouts page</h1>} />
              <Route path="workout-execution" element={<h1>Execution page</h1>} />
            </Route>
          </Routes>
        </ToastProvider>
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function openSettings() {
  await userEvent.click(await screen.findByRole('button', { name: 'Settings' }))
  return screen.findByRole('dialog', { name: 'Settings' })
}

describe('DashboardPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterAll(() => {
    MotionGlobalConfig.skipAnimations = false
  })

  beforeEach(() => {
    vi.mocked(getUserProfile).mockResolvedValue(PROFILE)
    vi.mocked(updateUserSettings).mockResolvedValue(undefined)
    mockWorkouts(0)
    vi.mocked(getSplit).mockResolvedValue(splitWith({}))
    vi.mocked(getWorkout).mockResolvedValue(pushDetails)
    vi.mocked(getLastSession).mockResolvedValue({ sessionId: null, startedAt: null, durationSeconds: null, exercises: [] })
  })

  afterEach(() => {
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
  })

  it('guides a new account through setting up the first week', async () => {
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Let’s set up your first week' })).toBeInTheDocument()
    expect(screen.getByText('Welcome, Rafael')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Create your first workout' })).toHaveAttribute('href', '/workouts')
  })

  it('shows today’s workout with its exercises in order', async () => {
    mockWorkouts(2)
    vi.mocked(getSplit).mockResolvedValue(splitWith({ [todayNumber()]: push }))
    vi.mocked(getLastSession).mockResolvedValue({
      sessionId: 's1',
      startedAt: '2026-11-04T10:00:00Z',
      durationSeconds: 100,
      exercises: [],
    })
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Push day' })).toBeInTheDocument()
    const items = within(screen.getByRole('list', { name: 'Exercises in today’s workout' })).getAllByRole('listitem')
    expect(items[0]).toHaveTextContent('Bench Press3 sets')
    expect(items[1]).toHaveTextContent('Cable Fly1 set')
    expect(screen.getByRole('link', { name: /Start workout/ })).toHaveAttribute('href', '/workout-execution')
    expect(screen.getByText(/Last session:/)).toBeInTheDocument()
  })

  it('still shows today’s workout when the last session cannot be loaded', async () => {
    mockWorkouts(2)
    vi.mocked(getSplit).mockResolvedValue(splitWith({ [todayNumber()]: push }))
    vi.mocked(getLastSession).mockRejectedValue(new ApiError(500, 'Boom'))
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Push day' })).toBeInTheDocument()
    expect(screen.queryByText(/Last session:/)).not.toBeInTheDocument()
  })

  it('omits the last session line before the first session', async () => {
    mockWorkouts(2)
    vi.mocked(getSplit).mockResolvedValue(splitWith({ [todayNumber()]: push }))
    renderHome()

    await screen.findByRole('heading', { level: 1, name: 'Push day' })

    expect(screen.queryByText(/Last session:/)).not.toBeInTheDocument()
  })

  it('shows the next scheduled workout on a rest day', async () => {
    mockWorkouts(2)
    vi.mocked(getSplit).mockResolvedValue(splitWith({ [otherDay()]: pull }))
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Rest day' })).toBeInTheDocument()
    expect(screen.getByText(/Pull day/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Start a workout anyway' })).toHaveAttribute('href', '/workout-execution')
    expect(screen.queryByRole('link', { name: 'Plan your week' })).not.toBeInTheDocument()
  })

  it('suggests planning the week when nothing is scheduled', async () => {
    mockWorkouts(2)
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Rest day' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Plan your week' })).toHaveAttribute('href', '/split')
  })

  it('offers a retry when the day cannot be loaded', async () => {
    vi.mocked(getSplit).mockRejectedValueOnce(new ApiError(500, 'Boom'))
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Could not load your day' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Let’s set up your first week' })).toBeInTheDocument()
  })

  it('links to the main areas', async () => {
    renderHome()
    await screen.findByRole('heading', { level: 1 })

    const jump = within(screen.getByRole('region', { name: 'Jump to' }))
    expect(jump.getByRole('link', { name: 'Workouts' })).toHaveAttribute('href', '/workouts')
    expect(jump.getByRole('link', { name: 'Training split' })).toHaveAttribute('href', '/split')
    expect(jump.getByRole('link', { name: 'Progression' })).toHaveAttribute('href', '/progress')
    expect(jump.getByRole('link', { name: 'Exercise library' })).toHaveAttribute('href', '/exercises')
  })

  it('navigates when a link is clicked', async () => {
    renderHome()

    await userEvent.click(within(await screen.findByRole('region', { name: 'Jump to' })).getByRole('link', { name: 'Workouts' }))

    expect(await screen.findByRole('heading', { name: 'Workouts page' })).toBeInTheDocument()
  })

  it('keeps the guide hidden until the How to use button is pressed', async () => {
    renderHome()
    await screen.findByRole('heading', { level: 1 })

    expect(screen.queryByText('Build a workout')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'How to use' }))

    const dialog = await screen.findByRole('dialog', { name: 'How to use GymRat' })
    expect(dialog).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 4 })).toHaveLength(4)

    await userEvent.click(screen.getByRole('button', { name: 'Close' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('shows the account and current settings in the settings modal', async () => {
    renderHome()

    await openSettings()

    expect(screen.getByText('Rafael Foica')).toBeInTheDocument()
    expect(screen.getByText('rafael@example.com')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Kg' })).toBeChecked()
    expect(screen.getByLabelText('Default rest time in seconds')).toHaveTextContent('90')
  })

  it('shows pounds as selected for users who prefer pounds', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, lastName: null, weightUnit: WeightUnit.Pounds })
    renderHome()

    await openSettings()

    expect(screen.getByRole('radio', { name: 'Lbs' })).toBeChecked()
    expect(screen.getByText('Rafael')).toBeInTheDocument()
  })

  it('saves the changed unit and rest time to the server', async () => {
    renderHome()
    await openSettings()

    await userEvent.click(screen.getByRole('radio', { name: 'Lbs' }))
    await userEvent.click(screen.getByRole('button', { name: 'Increase rest time' }))
    await userEvent.click(screen.getByRole('button', { name: 'Increase rest time' }))
    await userEvent.click(screen.getByRole('button', { name: 'Decrease rest time' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(updateUserSettings).toHaveBeenCalledWith({ weightUnit: WeightUnit.Pounds, defaultRestTimeSeconds: 105 }),
    )
    expect(await screen.findByText('Settings saved.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('saves kilograms when kilograms is selected', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, weightUnit: WeightUnit.Pounds })
    renderHome()
    await openSettings()

    await userEvent.click(screen.getByRole('radio', { name: 'Kg' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(updateUserSettings).toHaveBeenCalledWith({ weightUnit: WeightUnit.Kilograms, defaultRestTimeSeconds: 90 }),
    )
  })

  it('stops the rest time at the minimum and maximum', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, defaultRestTimeSeconds: 30 })
    renderHome()
    await openSettings()

    expect(screen.getByRole('button', { name: 'Decrease rest time' })).toBeDisabled()
  })

  it('caps the rest time at the maximum', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, defaultRestTimeSeconds: 595 })
    renderHome()
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Increase rest time' }))

    expect(screen.getByLabelText('Default rest time in seconds')).toHaveTextContent('600')
    expect(screen.getByRole('button', { name: 'Increase rest time' })).toBeDisabled()
  })

  it('keeps the modal open and shows an error when saving fails', async () => {
    vi.mocked(updateUserSettings).mockRejectedValue(new ApiError(500, 'Boom'))
    renderHome()
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText(/went wrong|try again|Boom/i)).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Settings' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeEnabled()
  })

  it('logs out and returns to the landing page', async () => {
    renderHome()
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }))

    expect(await screen.findByRole('heading', { level: 1, name: /Log workouts/ })).toBeInTheDocument()
    expect(localStorage.length).toBe(0)
    expect(login).not.toHaveBeenCalled()
  })
})

describe('HomePage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterAll(() => {
    MotionGlobalConfig.skipAnimations = false
  })

  afterEach(() => {
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
  })

  it('shows the landing page to guests', () => {
    render(
      <MemoryRouter>
        <AuthProvider>
          <HomePage />
        </AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { level: 1, name: /Log workouts/ })).toBeInTheDocument()
  })

  it('shows a spinner while the session is being restored', () => {
    vi.mocked(getUserProfile).mockReturnValue(new Promise(() => undefined))
    saveSession({ accessToken: 'token', expiresAt: '2999-01-01T00:00:00Z' })
    render(
      <MemoryRouter>
        <AuthProvider>
          <HomePage />
        </AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('status')).toBeInTheDocument()
  })
})

describe('SettingsModal without a session', () => {
  it('renders nothing when nobody is signed in', () => {
    render(
      <MemoryRouter>
        <AuthProvider>
          <ToastProvider>
            <SettingsModal open onClose={() => undefined} />
          </ToastProvider>
        </AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
