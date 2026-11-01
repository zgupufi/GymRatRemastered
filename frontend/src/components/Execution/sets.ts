import type { WeightUnit } from '../../api/auth'
import type { SessionSet, SessionSetEntry } from '../../api/sessions'
import { toKilograms } from '../../lib/weight'

export interface SetDraft {
  weight: string
  reps: string
  done: boolean
}

const DEFAULT_SETS = 3
const MAX_WEIGHT_KG = 2000
const MAX_REPS = 1000

export function emptyDrafts(targetSets: number): SetDraft[] {
  const count = targetSets > 0 ? targetSets : DEFAULT_SETS
  return Array.from({ length: count }, () => ({ weight: '', reps: '', done: false }))
}

export function isValidDraft(draft: SetDraft, unit: WeightUnit): boolean {
  if (draft.weight === '' || draft.reps === '') {
    return false
  }
  const weight = Number(draft.weight)
  const reps = Number(draft.reps)
  return (
    Number.isFinite(weight) &&
    Number.isInteger(reps) &&
    weight >= 0 &&
    toKilograms(weight, unit) <= MAX_WEIGHT_KG &&
    reps >= 1 &&
    reps <= MAX_REPS
  )
}

export function toEntries(drafts: SetDraft[], unit: WeightUnit): SessionSetEntry[] {
  return drafts
    .filter((draft) => draft.done && isValidDraft(draft, unit))
    .map((draft) => ({ reps: Number(draft.reps), weightKg: toKilograms(Number(draft.weight), unit) }))
}

export function previousFor(previous: SessionSet[] | undefined, setNumber: number): SessionSet | undefined {
  return previous?.find((set) => set.setNumber === setNumber)
}
