import type { WeightUnit } from '../../api/auth'
import type { ExerciseTarget, ProgressionReason } from '../../api/sessions'
import { displayWeight, unitLabel } from '../../lib/weight'

interface TargetBannerProps {
  target: ExerciseTarget
  unit: WeightUnit
  onUse: () => void
}

const REASON_LABELS: Record<ProgressionReason, string> = {
  NoHistory: 'First time',
  IncreaseWeight: 'New weight',
  AddRep: 'Add a rep',
  Hold: 'Hold',
  Stalled: 'Stalled',
}

const REASON_HINTS: Partial<Record<ProgressionReason, string>> = {
  Hold: 'Same weight and reps until it feels easier.',
  Stalled: 'Stalled two sessions in a row. Ask your coach for a plan.',
}

function formatWeight(weightKg: number, unit: WeightUnit): string {
  return weightKg === 0 ? 'Bodyweight' : `${displayWeight(weightKg, unit)} ${unitLabel(unit).toLowerCase()}`
}

export function TargetBanner({ target, unit, onUse }: TargetBannerProps) {
  const reason: ProgressionReason = target.reason in REASON_LABELS ? target.reason : 'AddRep'
  const hasWeight = typeof target.weightKg === 'number'
  const hint = REASON_HINTS[reason]

  return (
    <section className={`target-banner target-banner--${reason.toLowerCase()}`} aria-label="Today's target">
      <div className="target-banner__text">
        <small>
          Today's target <span className="target-banner__chip">{REASON_LABELS[reason]}</span>
        </small>
        <strong>
          {typeof target.weightKg === 'number'
            ? `${formatWeight(target.weightKg, unit)} × ${target.reps}`
            : `Pick a weight, aim for ${target.repsMin}–${target.repsMax}`}
        </strong>
        {typeof target.lastWeightKg === 'number' ? (
          <em>
            Last time {formatWeight(target.lastWeightKg, unit)} × {(target.lastReps ?? []).join(', ')}
          </em>
        ) : null}
        {hint ? <em>{hint}</em> : null}
      </div>
      {hasWeight ? (
        <button type="button" className="target-banner__use" onClick={onUse}>
          Use target
        </button>
      ) : null}
    </section>
  )
}
