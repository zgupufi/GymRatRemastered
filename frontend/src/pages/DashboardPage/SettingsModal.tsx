import { useState } from 'react'
import { LogOut, Minus, Plus } from 'lucide-react'
import { WeightUnit } from '../../api/auth'
import { friendlyErrorMessage } from '../../api/errors'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { SegmentedControl } from '../../components/ui/SegmentedControl'
import { useToast } from '../../components/ui/ToastContext'
import './DashboardPage.css'

export const MIN_REST_SECONDS = 30
export const MAX_REST_SECONDS = 600
export const REST_STEP_SECONDS = 15

type UnitKey = 'kg' | 'lbs'

const unitOptions: { value: UnitKey; label: string }[] = [
  { value: 'kg', label: 'Kg' },
  { value: 'lbs', label: 'Lbs' },
]

interface SettingsModalProps {
  open: boolean
  onClose: () => void
}

function SettingsForm({ onClose }: { onClose: () => void }) {
  const { user, updateSettings, logout } = useAuth()
  const { showToast } = useToast()
  const [unit, setUnit] = useState<UnitKey>(user?.weightUnit === WeightUnit.Pounds ? 'lbs' : 'kg')
  const [restSeconds, setRestSeconds] = useState(user?.defaultRestTimeSeconds ?? 90)
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
      showToast('success', 'Settings saved.')
      onClose()
    } catch (error) {
      showToast('error', friendlyErrorMessage(error))
      setSaving(false)
    }
  }

  const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ')

  return (
    <div className="settings">
      <div className="settings__who">
        <span className="settings__avatar" aria-hidden="true">
          {(user?.firstName ?? '?').slice(0, 1).toUpperCase()}
        </span>
        <div>
          <strong>{fullName}</strong>
          <span>{user?.email}</span>
        </div>
      </div>

      <div className="settings__field">
        <span className="settings__label">Weight unit</span>
        <SegmentedControl label="Weight unit" options={unitOptions} value={unit} onChange={setUnit} wide />
      </div>

      <div className="settings__field">
        <span className="settings__label">Default rest time</span>
        <div className="settings__stepper">
          <button
            type="button"
            aria-label="Decrease rest time"
            disabled={restSeconds <= MIN_REST_SECONDS}
            onClick={() => changeRest(-REST_STEP_SECONDS)}
          >
            <Minus size={18} aria-hidden="true" />
          </button>
          <output aria-label="Default rest time in seconds">
            {restSeconds} <small>seconds</small>
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

      <Button fullWidth loading={saving} onClick={save}>
        Save changes
      </Button>
      <Button fullWidth variant="danger" onClick={logout}>
        <LogOut size={16} aria-hidden="true" />
        Log out
      </Button>
    </div>
  )
}

export function SettingsModal({ open, onClose }: SettingsModalProps) {
  return (
    <Modal open={open} title="Settings" onClose={onClose}>
      <SettingsForm onClose={onClose} />
    </Modal>
  )
}
