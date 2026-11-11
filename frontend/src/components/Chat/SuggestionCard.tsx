import { useState } from 'react'
import { Check } from 'lucide-react'
import type { CoachSuggestion } from '../../api/coach'
import { friendlyErrorMessage } from '../../api/errors'
import { ApiError } from '../../api/client'
import type { SuggestionAction } from './useChat'

interface SuggestionCardProps {
  suggestion: CoachSuggestion
  onResolve: (id: string, action: SuggestionAction) => Promise<void>
}

interface Change {
  label: string
  from: string
  to: string
}

function changesOf(suggestion: CoachSuggestion): Change[] {
  const changes: Change[] = []
  if (suggestion.targetSets !== null && suggestion.targetSets !== suggestion.currentTargetSets) {
    changes.push({ label: 'Sets', from: String(suggestion.currentTargetSets), to: String(suggestion.targetSets) })
  }
  const repsMin = suggestion.repsMin ?? suggestion.currentRepsMin
  const repsMax = suggestion.repsMax ?? suggestion.currentRepsMax
  if (repsMin !== suggestion.currentRepsMin || repsMax !== suggestion.currentRepsMax) {
    changes.push({
      label: 'Reps',
      from: `${suggestion.currentRepsMin}–${suggestion.currentRepsMax}`,
      to: `${repsMin}–${repsMax}`,
    })
  }
  if (suggestion.weightStepKg !== null && suggestion.weightStepKg !== suggestion.currentWeightStepKg) {
    changes.push({ label: 'Step', from: `${suggestion.currentWeightStepKg} kg`, to: `${suggestion.weightStepKg} kg` })
  }
  return changes
}

function errorMessage(error: unknown): string {
  return error instanceof ApiError && error.code && error.status >= 400 && error.status < 500 ? error.message : friendlyErrorMessage(error)
}

const RESOLVED_TEXT: Record<Exclude<CoachSuggestion['status'], 'Pending'>, string> = {
  Applied: 'Applied',
  Dismissed: 'Dismissed',
  Expired: 'No longer available',
}

const STATUSES: CoachSuggestion['status'][] = ['Pending', 'Applied', 'Dismissed', 'Expired']

export function SuggestionCard({ suggestion: raw, onResolve }: SuggestionCardProps) {
  const suggestion = STATUSES.includes(raw.status) ? raw : { ...raw, status: 'Expired' as const }
  const [busy, setBusy] = useState<SuggestionAction | null>(null)
  const [error, setError] = useState<string | null>(null)
  const title = `${suggestion.exerciseName} · ${suggestion.workoutName}`

  const resolve = async (action: SuggestionAction) => {
    setBusy(action)
    setError(null)
    try {
      await onResolve(suggestion.id, action)
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setBusy(null)
    }
  }

  return (
    <section className={`coach__suggestion coach__suggestion--${suggestion.status.toLowerCase()}`} aria-label={`Suggested change for ${title}`}>
      {suggestion.status === 'Pending' ? <p className="coach__suggestion-eyebrow">Suggested change</p> : null}
      <p className="coach__suggestion-title">{title}</p>
      {suggestion.status === 'Pending' ? (
        <>
          <dl className="coach__suggestion-changes">
            {changesOf(suggestion).map((change) => (
              <div key={change.label}>
                <dt>{change.label}</dt>
                <dd>
                  <s>{change.from}</s> → <strong>{change.to}</strong>
                </dd>
              </div>
            ))}
          </dl>
          <p className="coach__suggestion-why">{suggestion.rationale}</p>
          {error ? (
            <p className="coach__suggestion-error" role="alert">
              {error}
            </p>
          ) : null}
          <div className="coach__suggestion-actions">
            <button type="button" className="coach__suggestion-apply" disabled={busy !== null} onClick={() => void resolve('apply')}>
              {busy === 'apply' ? 'Applying…' : 'Apply'}
            </button>
            <button type="button" className="coach__suggestion-dismiss" disabled={busy !== null} onClick={() => void resolve('dismiss')}>
              Not now
            </button>
          </div>
        </>
      ) : (
        <p className="coach__suggestion-resolved">
          {suggestion.status === 'Applied' ? <Check size={13} strokeWidth={3} aria-hidden="true" /> : null}
          {RESOLVED_TEXT[suggestion.status]}
        </p>
      )}
    </section>
  )
}
