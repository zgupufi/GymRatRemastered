import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, WeightUnit } from '../../api/auth'
import { ApiError, setAccessToken } from '../../api/client'
import { getUserProfile, updateUserSettings, type UserProfile } from '../../api/users'
import { AuthProvider } from '../../auth/AuthProvider'
import { saveSession } from '../../auth/sessionStorage'
import { ToastProvider } from '../../components/ui/Toast'
import { HomePage } from '../HomePage'
import { SettingsModal } from './SettingsModal'

vi.mock('../../api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/auth')>()),
  login: vi.fn(),
}))
vi.mock('../../api/users')

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
            <Route index element={<HomePage />} />
            <Route path="workouts" element={<h1>Workouts page</h1>} />
            <Route path="workout-execution" element={<h1>Execution page</h1>} />
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
  })

  afterEach(() => {
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
  })

  it('shows the hero and the four core modules', async () => {
    renderHome()

    expect(await screen.findByRole('heading', { level: 1, name: 'Ready to crush your goals?' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Start Workout/ })).toHaveAttribute('href', '/workout-execution')
    expect(screen.getByRole('link', { name: /Workouts/ })).toHaveAttribute('href', '/workouts')
    expect(screen.getByRole('link', { name: /Training Split/ })).toHaveAttribute('href', '/split')
    expect(screen.getByRole('link', { name: /Progression/ })).toHaveAttribute('href', '/progress')
    expect(screen.getByRole('link', { name: /Exercise DB/ })).toHaveAttribute('href', '/exercises')
  })

  it('navigates when a module is clicked', async () => {
    renderHome()

    await userEvent.click(await screen.findByRole('link', { name: /Workouts/ }))

    expect(await screen.findByRole('heading', { name: 'Workouts page' })).toBeInTheDocument()
  })

  it('keeps the guide hidden until the How to use button is pressed', async () => {
    renderHome()
    await screen.findByRole('heading', { level: 1 })

    expect(screen.queryByText('Database Mastery')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'How to use GymRat' }))

    const dialog = await screen.findByRole('dialog', { name: 'How to use GymRat' })
    expect(dialog).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 4 })).toHaveLength(7)
    expect(screen.queryByText(/GymRat Mode/)).not.toBeInTheDocument()

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
