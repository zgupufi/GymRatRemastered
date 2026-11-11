import { Check, Minus, Plus } from 'lucide-react'
import { motion } from 'motion/react'
import type { WeightUnit } from '../../api/auth'
import type { ExerciseTarget, SessionSet } from '../../api/sessions'
import { press, spring } from '../../lib/motion'
import { displayWeight, unitLabel } from '../../lib/weight'
import { NumberInput } from '../ui/NumberInput'
import { canRemoveLast, EFFORT_OPTIONS, isValidDraft, MAX_SETS, previousFor, type SetDraft } from './sets'

interface SetsCardProps {
  drafts: SetDraft[]
  previous: SessionSet[] | undefined
  target?: ExerciseTarget
  unit: WeightUnit
  onChange: (index: number, patch: Partial<SetDraft>) => void
  onToggle: (index: number) => void
  onAdd: () => void
  onRemove: () => void
}

function placeholders(target: ExerciseTarget | undefined, last: SessionSet | undefined, unit: WeightUnit) {
  if (target && target.weightKg !== null) {
    return { weight: String(displayWeight(target.weightKg, unit)), reps: String(target.reps) }
  }
  if (last) {
    return { weight: String(displayWeight(last.weightKg, unit)), reps: String(last.reps) }
  }
  return { weight: '-', reps: target ? String(target.reps) : '-' }
}

export function SetsCard({ drafts, previous, target, unit, onChange, onToggle, onAdd, onRemove }: SetsCardProps) {
  const firstOpen = drafts.findIndex((draft) => !draft.done)
  return (
    <section className="sets-card" aria-label="Sets">
      <div className="sets-card__columns" aria-hidden="true">
        <span>Set</span>
        <span>{unitLabel(unit)}</span>
        <span>Reps</span>
        <span />
      </div>
      {drafts.map((draft, index) => {
        const number = index + 1
        const hint = placeholders(target, previousFor(previous, number), unit)
        const ready = isValidDraft(draft, unit) && drafts.slice(0, index).every((earlier) => earlier.done)
        const locked = draft.done && drafts.slice(index + 1).some((later) => later.done)
        return (
          <div key={number} className={`sets-card__set${draft.done ? ' sets-card__set--done' : ''}`}>
            <div className={`sets-card__row${draft.done ? ' sets-card__row--done' : ''}`}>
              <span className="sets-card__number">{number}</span>
              <NumberInput
                aria-label={`Set ${number} weight`}
                value={draft.weight}
                placeholder={hint.weight}
                disabled={draft.done}
                onValueChange={(weight) => onChange(index, { weight })}
              />
              <NumberInput
                aria-label={`Set ${number} reps`}
                value={draft.reps}
                placeholder={hint.reps}
                disabled={draft.done}
                onValueChange={(reps) => onChange(index, { reps })}
              />
              <motion.button
                type="button"
                className="sets-card__check"
                aria-label={draft.done ? `Undo set ${number}` : `Complete set ${number}`}
                aria-pressed={draft.done}
                disabled={draft.done ? locked : !ready}
                whileTap={press}
                transition={spring}
                onClick={() => onToggle(index)}
              >
                <Check size={18} aria-hidden="true" />
              </motion.button>
            </div>
            {draft.done || index === firstOpen ? (
              <div className="sets-card__effort" role="group" aria-label={`Effort for set ${number}`}>
                <small aria-hidden="true">Effort</small>
                {EFFORT_OPTIONS.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    className="sets-card__effort-option"
                    aria-pressed={draft.effort === option.value}
                    onClick={() => onChange(index, { effort: draft.effort === option.value ? null : option.value })}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        )
      })}
      <div className="sets-card__actions">
        <button type="button" className="sets-card__action" disabled={drafts.length >= MAX_SETS} onClick={onAdd}>
          <Plus size={16} aria-hidden="true" />
          Add set
        </button>
        <button type="button" className="sets-card__action" disabled={!canRemoveLast(drafts)} onClick={onRemove}>
          <Minus size={16} aria-hidden="true" />
          Remove last
        </button>
      </div>
    </section>
  )
}
