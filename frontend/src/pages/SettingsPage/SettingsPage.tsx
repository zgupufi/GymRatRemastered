import { useState, type FormEvent } from 'react'
import { ChevronRight, Minus, Plus } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { WeightUnit } from '../../api/auth'
import { friendlyErrorMessage } from '../../api/errors'
import { changePassword, deleteAccount, downloadExport, downloadSetsCsv, type UserProfile } from '../../api/users'
import { useAuth } from '../../auth/AuthContext'
import { validateLoginPassword, validateNewPassword } from '../../auth/validation'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { SegmentedControl } from '../../components/ui/SegmentedControl'
import { Switch } from '../../components/ui/Switch'
import { useToast } from '../../components/ui/ToastContext'
import { saveBlob } from '../../lib/download'
import { readHapticsEnabled, writeHapticsEnabled } from '../../lib/haptics'
import './SettingsPage.css'

export const MIN_REST_SECONDS = 30
export const MAX_REST_SECONDS = 600
export const REST_STEP_SECONDS = 15

type UnitKey = 'kg' | 'lbs'

const unitOptions: { value: UnitKey; label: string }[] = [
  { value: 'kg', label: 'Kg' },
  { value: 'lbs', label: 'Lbs' },
]

function fullNameOf(user: UserProfile): string {
  return [user.firstName, user.lastName].filter(Boolean).join(' ')
}

function initialsOf(user: UserProfile): string {
  return [user.firstName, user.lastName]
    .filter(Boolean)
    .map((name) => (name as string).slice(0, 1).toUpperCase())
    .join('')
}

function ProfileSection({ user }: { user: UserProfile }) {
  return (
    <section className="settings-page__section" aria-labelledby="settings-profile">
      <h2 id="settings-profile">Profile</h2>
      <div className="settings-page__group">
        <div className="settings-page__row settings-page__row--profile">
          <span className="settings-page__avatar" aria-hidden="true">
            {initialsOf(user)}
          </span>
          <div className="settings-page__text">
            <strong>{fullNameOf(user)}</strong>
            <span className="settings-page__hint">{user.email}</span>
          </div>
        </div>
      </div>
    </section>
  )
}

function PreferencesSection({ user }: { user: UserProfile }) {
  const { updateSettings } = useAuth()
  const { showToast } = useToast()
  const [unit, setUnit] = useState<UnitKey>(user.weightUnit === WeightUnit.Pounds ? 'lbs' : 'kg')
  const [restSeconds, setRestSeconds] = useState(user.defaultRestTimeSeconds)
  const [haptics, setHaptics] = useState(readHapticsEnabled)
  const [saving, setSaving] = useState(false)

  const changeRest = (delta: number) => {
    setRestSeconds((current) => Math.min(MAX_REST_SECONDS, Math.max(MIN_REST_SECONDS, current + delta)))
  }

  const save = async () => {
    setSaving(true)
    try {
      await updateSettings({
        weightUnit: unit === 'lbs' ? WeightUnit.Pounds : WeightUnit.Kilograms,
        defaultRestTimeSeconds: restSeconds,
      })
      writeHapticsEnabled(haptics)
      showToast('success', 'Settings saved.')
    } catch (error) {
      showToast('error', friendlyErrorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="settings-page__section" aria-labelledby="settings-preferences">
      <h2 id="settings-preferences">Preferences</h2>
      <div className="settings-page__group">
        <div className="settings-page__row">
          <span className="settings-page__key">Weight unit</span>
          <div>
            <SegmentedControl label="Weight unit" options={unitOptions} value={unit} onChange={setUnit} />
          </div>
        </div>
        <div className="settings-page__row">
          <div className="settings-page__text">
            <span className="settings-page__key">
              Default rest<span className="settings-page__long"> time</span>
            </span>
            <span className="settings-page__hint">Between sets</span>
          </div>
          <div className="settings-page__stepper">
            <button
              type="button"
              aria-label="Decrease rest time"
              disabled={restSeconds <= MIN_REST_SECONDS}
              onClick={() => changeRest(-REST_STEP_SECONDS)}
            >
              <Minus size={18} aria-hidden="true" />
            </button>
            <output aria-label="Default rest time in seconds">
              {restSeconds} <small>sec</small>
            </output>
            <button
              type="button"
              aria-label="Increase rest time"
              disabled={restSeconds >= MAX_REST_SECONDS}
              onClick={() => changeRest(REST_STEP_SECONDS)}
            >
              <Plus size={18} aria-hidden="true" />
            </button>
          </div>
        </div>
        <div className="settings-page__row">
          <div className="settings-page__text">
            <span className="settings-page__key">Vibration</span>
            <span className="settings-page__hint">
              <span className="settings-page__long">Completed set, finished workout and end of rest, on phones that support it</span>
              <span className="settings-page__short">Sets, finish and rest end</span>
            </span>
          </div>
          <Switch label="Vibration" checked={haptics} onChange={setHaptics} />
        </div>
      </div>
      <div className="settings-page__save">
        <Button loading={saving} onClick={save}>
          Save changes
        </Button>
      </div>
    </section>
  )
}

function DataSection() {
  const { showToast } = useToast()
  const [busy, setBusy] = useState<'json' | 'csv' | null>(null)

  const download = async (kind: 'json' | 'csv') => {
    setBusy(kind)
    try {
      const blob = await (kind === 'json' ? downloadExport() : downloadSetsCsv())
      saveBlob(blob, kind === 'json' ? 'gymrat-export.json' : 'gymrat-sets.csv')
    } catch (error) {
      showToast('error', friendlyErrorMessage(error))
    } finally {
      setBusy(null)
    }
  }

  return (
    <section className="settings-page__section" aria-labelledby="settings-data">
      <h2 id="settings-data">Data</h2>
      <div className="settings-page__group">
        <div className="settings-page__row">
          <div className="settings-page__text">
            <span className="settings-page__key">Export everything</span>
            <span className="settings-page__hint">Profile, workouts, split and every session with its sets (JSON)</span>
          </div>
          <Button
            variant="secondary"
            size="sm"
            loading={busy === 'json'}
            aria-label="Download JSON"
            disabled={busy !== null}
            onClick={() => download('json')}
          >
            Download<span className="settings-page__long"> JSON</span>
          </Button>
        </div>
        <div className="settings-page__row">
          <div className="settings-page__text">
            <span className="settings-page__key">Export sets</span>
            <span className="settings-page__hint">One row per logged set, opens in a spreadsheet (CSV)</span>
          </div>
          <Button
            variant="secondary"
            size="sm"
            loading={busy === 'csv'}
            aria-label="Download CSV"
            disabled={busy !== null}
            onClick={() => download('csv')}
          >
            Download<span className="settings-page__long"> CSV</span>
          </Button>
        </div>
      </div>
    </section>
  )
}

interface PasswordErrors {
  current?: string
  next?: string
  confirm?: string
}

function PasswordForm({ onCancel, onChanged }: { onCancel: () => void; onChanged: () => void }) {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<PasswordErrors>({})
  const [saving, setSaving] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const found: PasswordErrors = {
      current: validateLoginPassword(current),
      next: validateNewPassword(next),
      confirm: confirm === next ? undefined : 'The passwords do not match.',
    }
    setErrors(found)
    if (found.current || found.next || found.confirm) {
      return
    }
    setSaving(true)
    try {
      await changePassword(current, next)
      onChanged()
    } catch (error) {
      setErrors({ current: friendlyErrorMessage(error) })
      setSaving(false)
    }
  }

  return (
    <form className="settings-page__form" onSubmit={submit} noValidate>
      <Input
        label="Current password"
        type="password"
        autoComplete="current-password"
        value={current}
        error={errors.current}
        onChange={(event) => setCurrent(event.target.value)}
      />
      <Input
        label="New password"
        type="password"
        autoComplete="new-password"
        value={next}
        error={errors.next}
        onChange={(event) => setNext(event.target.value)}
      />
      <Input
        label="Confirm new password"
        type="password"
        autoComplete="new-password"
        value={confirm}
        error={errors.confirm}
        onChange={(event) => setConfirm(event.target.value)}
      />
      <div className="settings-page__actions">
        <Button variant="secondary" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" size="sm" loading={saving}>
          Update password
        </Button>
      </div>
    </form>
  )
}

function AccountSection() {
  const { logout } = useAuth()
  const { showToast } = useToast()
  const navigate = useNavigate()
  const [changing, setChanging] = useState(false)

  const passwordChanged = () => {
    logout()
    showToast('success', 'Password changed. Log in again with the new one.')
    void navigate('/login')
  }

  return (
    <section className="settings-page__section" aria-labelledby="settings-account">
      <h2 id="settings-account">Account</h2>
      <div className="settings-page__group">
        <button
          type="button"
          className="settings-page__row settings-page__row--button"
          aria-expanded={changing}
          onClick={() => setChanging((open) => !open)}
        >
          <span className="settings-page__key">Change password</span>
          <ChevronRight
            size={18}
            aria-hidden="true"
            className={changing ? 'settings-page__chevron--open' : undefined}
          />
        </button>
        {changing ? <PasswordForm onCancel={() => setChanging(false)} onChanged={passwordChanged} /> : null}
        <button type="button" className="settings-page__row settings-page__row--button" onClick={logout}>
          <span className="settings-page__key">Log out</span>
          <ChevronRight size={18} aria-hidden="true" />
        </button>
      </div>
      <p className="settings-page__note">Changing your password signs you out on every device, including this one.</p>
    </section>
  )
}

function DeleteDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { logout } = useAuth()
  const { showToast } = useToast()
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string>()
  const [deleting, setDeleting] = useState(false)

  const close = () => {
    setPassword('')
    setError(undefined)
    onClose()
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const problem = validateLoginPassword(password)
    setError(problem)
    if (problem) {
      return
    }
    setDeleting(true)
    try {
      await deleteAccount(password)
      logout()
      showToast('success', 'Your account was deleted.')
    } catch (failure) {
      setError(friendlyErrorMessage(failure))
      setDeleting(false)
    }
  }

  return (
    <Modal open={open} title="Delete your account?" onClose={close}>
      <form className="settings-page__dialog" onSubmit={submit} noValidate>
        <p>This permanently deletes:</p>
        <ul>
          <li>Your profile and settings</li>
          <li>All workouts and your split</li>
          <li>Every session and logged set</li>
          <li>Coach suggestions</li>
        </ul>
        <Input
          label="Enter your current password to confirm"
          type="password"
          autoComplete="current-password"
          value={password}
          error={error}
          onChange={(event) => setPassword(event.target.value)}
        />
        <div className="settings-page__actions">
          <Button variant="secondary" size="sm" onClick={close}>
            Cancel
          </Button>
          <Button type="submit" variant="danger" size="sm" loading={deleting}>
            Delete account
          </Button>
        </div>
      </form>
    </Modal>
  )
}

function DangerSection() {
  const [confirming, setConfirming] = useState(false)

  return (
    <section className="settings-page__section" aria-labelledby="settings-danger">
      <h2 id="settings-danger" className="settings-page__danger-title">
        Danger zone
      </h2>
      <div className="settings-page__group settings-page__group--danger">
        <div className="settings-page__row settings-page__row--danger">
          <div className="settings-page__text">
            <span className="settings-page__key">Delete account</span>
            <span className="settings-page__hint">
              Permanently removes your profile, workouts, split, sessions and coach suggestions. This cannot be undone.
            </span>
          </div>
          <Button variant="danger" size="sm" onClick={() => setConfirming(true)}>
            Delete account
          </Button>
        </div>
      </div>
      <DeleteDialog open={confirming} onClose={() => setConfirming(false)} />
    </section>
  )
}

export function SettingsPage() {
  const { user } = useAuth()
  if (!user) {
    return null
  }

  return (
    <main className="page settings-page">
      <BackButton />
      <header className="page__header">
        <h1>Settings</h1>
      </header>
      <ProfileSection user={user} />
      <PreferencesSection user={user} />
      <DataSection />
      <AccountSection />
      <DangerSection />
    </main>
  )
}
