import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { WeightUnit } from '../../api/auth'
import { ApiError, setAccessToken } from '../../api/client'
import {
  changePassword,
  deleteAccount,
  downloadExport,
  downloadSetsCsv,
  getUserProfile,
  updateUserSettings,
  type UserProfile,
} from '../../api/users'
import { AuthProvider } from '../../auth/AuthProvider'
import { RequireAuth } from '../../auth/RequireAuth'
import { ToastProvider } from '../../components/ui/Toast'
import { saveBlob } from '../../lib/download'
import { givenRestorableSession } from '../../test/sessionFixture'
import { SettingsPage } from './SettingsPage'
import { expectNoA11yViolations } from '../../test/a11y'

vi.mock('../../api/users')
vi.mock('../../lib/download')

const PROFILE: UserProfile = {
  firstName: 'Rafael',
  lastName: 'Foica',
  email: 'rafael@example.com',
  weightUnit: WeightUnit.Kilograms,
  defaultRestTimeSeconds: 90,
}

function LoginProbe() {
  const location = useLocation()
  return <h1 data-from={JSON.stringify(location.state)}>Login page</h1>
}

function renderSettings() {
  givenRestorableSession()
  return render(
    <MemoryRouter initialEntries={['/settings']}>
      <AuthProvider>
        <ToastProvider>
          <Routes>
            <Route element={<RequireAuth />}>
              <Route path="settings" element={<SettingsPage />} />
            </Route>
            <Route path="login" element={<LoginProbe />} />
          </Routes>
        </ToastProvider>
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function openSettings() {
  renderSettings()
  await screen.findByRole('heading', { level: 1, name: 'Settings' })
}

describe('SettingsPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterAll(() => {
    MotionGlobalConfig.skipAnimations = false
  })

  beforeEach(() => {
    vi.mocked(getUserProfile).mockResolvedValue(PROFILE)
    vi.mocked(updateUserSettings).mockResolvedValue(undefined)
    vi.mocked(changePassword).mockResolvedValue(undefined)
    vi.mocked(deleteAccount).mockResolvedValue(undefined)
  })

  afterEach(() => {
    cleanup()
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
  })

  it('shows the account and the current settings', async () => {
    await openSettings()

    expect(screen.getByText('Rafael Foica')).toBeInTheDocument()
    expect(screen.getByText('rafael@example.com')).toBeInTheDocument()
    expect(screen.getByText('RF')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Kg' })).toBeChecked()
    expect(screen.getByLabelText('Default rest time in seconds')).toHaveTextContent('90')
    for (const name of ['Profile', 'Preferences', 'Data', 'Account', 'Danger zone']) {
      expect(screen.getByRole('heading', { level: 2, name })).toBeInTheDocument()
    }
  })

  it('shows pounds as selected for users who prefer pounds', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, lastName: null, weightUnit: WeightUnit.Pounds })

    await openSettings()

    expect(screen.getByRole('radio', { name: 'Lbs' })).toBeChecked()
    expect(screen.getByText('Rafael')).toBeInTheDocument()
  })

  it('renders nothing when nobody is signed in', () => {
    render(
      <MemoryRouter>
        <AuthProvider>
          <ToastProvider>
            <SettingsPage />
          </ToastProvider>
        </AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.queryByRole('heading', { name: 'Settings' })).not.toBeInTheDocument()
  })

  it('saves the changed unit and rest time and stays on the page', async () => {
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
    expect(screen.getByRole('heading', { level: 1, name: 'Settings' })).toBeInTheDocument()
  })

  it('saves kilograms when kilograms is selected', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, weightUnit: WeightUnit.Pounds })
    await openSettings()

    await userEvent.click(screen.getByRole('radio', { name: 'Kg' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(updateUserSettings).toHaveBeenCalledWith({ weightUnit: WeightUnit.Kilograms, defaultRestTimeSeconds: 90 }),
    )
  })

  it('stops the rest time at the minimum', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, defaultRestTimeSeconds: 30 })
    await openSettings()

    expect(screen.getByRole('button', { name: 'Decrease rest time' })).toBeDisabled()
  })

  it('caps the rest time at the maximum', async () => {
    vi.mocked(getUserProfile).mockResolvedValue({ ...PROFILE, defaultRestTimeSeconds: 595 })
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Increase rest time' }))

    expect(screen.getByLabelText('Default rest time in seconds')).toHaveTextContent('600')
    expect(screen.getByRole('button', { name: 'Increase rest time' })).toBeDisabled()
  })

  it('shows an error and keeps the button usable when saving fails', async () => {
    vi.mocked(updateUserSettings).mockRejectedValue(new ApiError(500, 'Boom'))
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText(/went wrong|try again|Boom/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeEnabled()
  })

  it('downloads the JSON export', async () => {
    const blob = new Blob(['{}'])
    vi.mocked(downloadExport).mockResolvedValue(blob)
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Download JSON' }))

    await waitFor(() => expect(saveBlob).toHaveBeenCalledWith(blob, 'gymrat-export.json'))
  })

  it('downloads the sets as CSV', async () => {
    const blob = new Blob(['a,b'])
    vi.mocked(downloadSetsCsv).mockResolvedValue(blob)
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Download CSV' }))

    await waitFor(() => expect(saveBlob).toHaveBeenCalledWith(blob, 'gymrat-sets.csv'))
  })

  it('shows an error when a download fails', async () => {
    vi.mocked(downloadExport).mockRejectedValue(new ApiError(500, 'Boom'))
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Download JSON' }))

    expect(await screen.findByText(/went wrong|try again|issue/i)).toBeInTheDocument()
    expect(saveBlob).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Download JSON' })).toBeEnabled()
  })

  it('logs out and sends the user to the login page', async () => {
    await openSettings()

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }))

    await waitFor(() => expect(localStorage.length).toBe(0))
  })

  describe('change password', () => {
    async function openForm() {
      await openSettings()
      await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
    }

    it('keeps the form hidden until it is opened and can be cancelled', async () => {
      await openSettings()
      expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()

      await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
      expect(screen.getByLabelText('Current password')).toBeInTheDocument()

      await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
      expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()
    })

    it('validates every field before sending anything', async () => {
      await openForm()

      await userEvent.click(screen.getByRole('button', { name: 'Update password' }))

      expect(screen.getByText('Enter your password.')).toBeInTheDocument()
      expect(screen.getByText('Choose a password.')).toBeInTheDocument()
      expect(changePassword).not.toHaveBeenCalled()
    })

    it('rejects a confirmation that does not match', async () => {
      await openForm()
      await userEvent.type(screen.getByLabelText('Current password'), 'old-password')
      await userEvent.type(screen.getByLabelText('New password'), 'new-password-1')
      await userEvent.type(screen.getByLabelText('Confirm new password'), 'new-password-2')

      await userEvent.click(screen.getByRole('button', { name: 'Update password' }))

      expect(screen.getByText('The passwords do not match.')).toBeInTheDocument()
      expect(changePassword).not.toHaveBeenCalled()
    })

    it('changes the password and sends the user to log in again', async () => {
      await openForm()
      await userEvent.type(screen.getByLabelText('Current password'), 'old-password')
      await userEvent.type(screen.getByLabelText('New password'), 'new-password-1')
      await userEvent.type(screen.getByLabelText('Confirm new password'), 'new-password-1')

      await userEvent.click(screen.getByRole('button', { name: 'Update password' }))

      await waitFor(() => expect(changePassword).toHaveBeenCalledWith('old-password', 'new-password-1'))
      expect(await screen.findByRole('heading', { level: 1, name: 'Login page' })).toBeInTheDocument()
      expect(await screen.findByText('Password changed. Log in again with the new one.')).toBeInTheDocument()
    })

    it('shows the server error when the current password is wrong', async () => {
      vi.mocked(changePassword).mockRejectedValue(
        new ApiError(400, 'The password is not correct.', 'BLException_UsersService_InvalidPassword'),
      )
      await openForm()
      await userEvent.type(screen.getByLabelText('Current password'), 'wrong-password')
      await userEvent.type(screen.getByLabelText('New password'), 'new-password-1')
      await userEvent.type(screen.getByLabelText('Confirm new password'), 'new-password-1')

      await userEvent.click(screen.getByRole('button', { name: 'Update password' }))

      expect(await screen.findByText('The password is not correct.')).toBeInTheDocument()
      expect(screen.getByRole('heading', { level: 1, name: 'Settings' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Update password' })).toBeEnabled()
    })
  })

  describe('delete account', () => {
    async function openDialog() {
      await openSettings()
      await userEvent.click(screen.getByRole('button', { name: 'Delete account' }))
      return screen.findByRole('dialog', { name: 'Delete your account?' })
    }

    it('lists what is deleted and can be cancelled', async () => {
      const dialog = await openDialog()

      expect(within(dialog).getByText('Every session and logged set')).toBeInTheDocument()

      await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(deleteAccount).not.toHaveBeenCalled()
    })

    it('asks for the password before deleting', async () => {
      const dialog = await openDialog()

      await userEvent.click(within(dialog).getByRole('button', { name: 'Delete account' }))

      expect(within(dialog).getByText('Enter your password.')).toBeInTheDocument()
      expect(deleteAccount).not.toHaveBeenCalled()
    })

    it('deletes the account with the password and sends the user to the login page', async () => {
      const dialog = await openDialog()
      await userEvent.type(within(dialog).getByLabelText('Enter your current password to confirm'), 'my-password')

      await userEvent.click(within(dialog).getByRole('button', { name: 'Delete account' }))

      await waitFor(() => expect(deleteAccount).toHaveBeenCalledWith('my-password'))
      expect(await screen.findByRole('heading', { level: 1, name: 'Login page' })).toBeInTheDocument()
      expect(await screen.findByText('Your account was deleted.')).toBeInTheDocument()
      await waitFor(() =>
        expect(screen.getByRole('heading', { level: 1, name: 'Login page' })).toHaveAttribute('data-from', '{}'),
      )
    })

    it('keeps the dialog open and shows the error when the password is wrong', async () => {
      vi.mocked(deleteAccount).mockRejectedValue(
        new ApiError(400, 'The password is not correct.', 'BLException_UsersService_InvalidPassword'),
      )
      const dialog = await openDialog()
      await userEvent.type(within(dialog).getByLabelText('Enter your current password to confirm'), 'wrong')

      await userEvent.click(within(dialog).getByRole('button', { name: 'Delete account' }))

      expect(await within(dialog).findByText('The password is not correct.')).toBeInTheDocument()
      expect(within(dialog).getByRole('button', { name: 'Delete account' })).toBeEnabled()
    })

    it('clears the typed password and error after closing', async () => {
      const dialog = await openDialog()
      await userEvent.click(within(dialog).getByRole('button', { name: 'Delete account' }))
      await userEvent.type(within(dialog).getByLabelText('Enter your current password to confirm'), 'abc')
      await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

      const reopened = await (async () => {
        await userEvent.click(screen.getByRole('button', { name: 'Delete account' }))
        return screen.findByRole('dialog', { name: 'Delete your account?' })
      })()

      expect(within(reopened).getByLabelText('Enter your current password to confirm')).toHaveValue('')
      expect(within(reopened).queryByText('Enter your password.')).not.toBeInTheDocument()
    })
  })

  it('has no accessibility violations', async () => {
    await openSettings()

    await expectNoA11yViolations()
  })
})
