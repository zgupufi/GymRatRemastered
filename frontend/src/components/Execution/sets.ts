import type { WeightUnit } from '../../api/auth'
import type { ExerciseTarget, SessionSet, SessionSetEntry } from '../../api/sessions'
import { displayWeight, toKilograms } from '../../lib/weight'

export interface SetDraft {
  weight: string
  reps: string
  done: boolean
  effort: number | null
}

const DEFAULT_SETS = 3
const MAX_WEIGHT_KG = 2000
const MAX_REPS = 1000
export const MAX_SETS = 50

export const EFFORT_OPTIONS = [
  { value: 3, label: 'Easy' },
  { value: 2, label: 'Good' },
  { value: 1, label: 'Hard' },
  { value: 0, label: 'Max' },
] as const

export function effortLabel(repsInReserve: number | null | undefined): string | null {
  return EFFORT_OPTIONS.find((option) => option.value === repsInReserve)?.label ?? null
}

export function emptyDraft(): SetDraft {
  return { weight: '', reps: '', done: false, effort: null }
}

export function emptyDrafts(targetSets: number): SetDraft[] {
  const count = targetSets > 0 ? targetSets : DEFAULT_SETS
  return Array.from({ length: count }, emptyDraft)
}

export function draftsFromSaved(targetSets: number, saved: SessionSet[] | undefined, unit: WeightUnit): SetDraft[] {
  const drafts = emptyDrafts(targetSets)
  const sets = [...(saved ?? [])].sort((a, b) => a.setNumber - b.setNumber)
  while (drafts.length < sets.length) {
    drafts.push(emptyDraft())
  }
  sets.forEach((set, position) => {
    drafts[position] = {
      weight: String(displayWeight(set.weightKg, unit)),
      reps: String(set.reps),
      done: true,
      effort: set.repsInReserve ?? null,
    }
  })
  return drafts
}

export function addDraft(drafts: SetDraft[]): SetDraft[] {
  if (drafts.length >= MAX_SETS) {
    return drafts
  }
  const last = drafts[drafts.length - 1]
  return [...drafts, { ...emptyDraft(), weight: last?.weight ?? '', reps: last?.reps ?? '' }]
}

export function canRemoveLast(drafts: SetDraft[]): boolean {
  return drafts.length > 1 && !drafts[drafts.length - 1].done
}

export function removeLastDraft(drafts: SetDraft[]): SetDraft[] {
  return canRemoveLast(drafts) ? drafts.slice(0, -1) : drafts
}

export function applyTarget(drafts: SetDraft[], target: ExerciseTarget, unit: WeightUnit): SetDraft[] {
  const weight = target.weightKg === null ? null : String(displayWeight(target.weightKg, unit))
  return drafts.map((draft) =>
    draft.done ? draft : { ...draft, weight: weight ?? draft.weight, reps: String(target.reps) },
  )
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
    .map((draft) => ({
      reps: Number(draft.reps),
      weightKg: toKilograms(Number(draft.weight), unit),
      ...(draft.effort === null ? {} : { repsInReserve: draft.effort }),
    }))
}

export function previousFor(previous: SessionSet[] | undefined, setNumber: number): SessionSet | undefined {
  return previous?.find((set) => set.setNumber === setNumber)
}

export interface ExerciseResult {
  name: string
  best: { weightKg: number; reps: number } | null
}

export interface WorkoutResult {
  sets: number
  volumeKg: number
  exercises: ExerciseResult[]
}

function estimatedOneRepMax(entry: SessionSetEntry): number {
  return entry.weightKg * (1 + entry.reps / 30)
}

export function summarize(
  exercises: { id: string; name: string }[],
  drafts: Record<string, SetDraft[]>,
  unit: WeightUnit,
): WorkoutResult {
  let sets = 0
  let volumeKg = 0
  const results = exercises.map((exercise) => {
    const entries = toEntries(drafts[exercise.id] ?? [], unit)
    let best: SessionSetEntry | null = null
    for (const entry of entries) {
      sets += 1
      volumeKg += entry.weightKg * entry.reps
      if (best === null || estimatedOneRepMax(entry) > estimatedOneRepMax(best)) {
        best = entry
      }
    }
    return { name: exercise.name, best }
  })
  return { sets, volumeKg, exercises: results.filter((result) => result.best !== null) }
}
