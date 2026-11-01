import { Check } from 'lucide-react'
import { motion } from 'motion/react'
import type { WeightUnit } from '../../api/auth'
import type { SessionSet } from '../../api/sessions'
import { press, spring } from '../../lib/motion'
import { fromKilograms, unitLabel } from '../../lib/weight'
import { NumberInput } from '../ui/NumberInput'
import { isValidDraft, previousFor, type SetDraft } from './sets'

interface SetsCardProps {
  drafts: SetDraft[]
  previous: SessionSet[] | undefined
  unit: WeightUnit
  onChange: (index: number, patch: Partial<SetDraft>) => void
  onToggle: (index: number) => void
}

export function SetsCard({ drafts, previous, unit, onChange, onToggle }: SetsCardProps) {
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
        const last = previousFor(previous, number)
        const ready = isValidDraft(draft, unit) && drafts.slice(0, index).every((earlier) => earlier.done)
        const locked = draft.done && drafts.slice(index + 1).some((later) => later.done)
        return (
          <div key={number} className={`sets-card__row${draft.done ? ' sets-card__row--done' : ''}`}>
            <span className="sets-card__number">{number}</span>
            <NumberInput
              aria-label={`Set ${number} weight`}
              value={draft.weight}
              placeholder={last ? String(fromKilograms(last.weightKg, unit)) : '-'}
              disabled={draft.done}
              onValueChange={(weight) => onChange(index, { weight })}
            />
            <NumberInput
              aria-label={`Set ${number} reps`}
              value={draft.reps}
              placeholder={last ? String(last.reps) : '-'}
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
        )
      })}
    </section>
  )
}
