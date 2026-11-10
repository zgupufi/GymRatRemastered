import { describe, expect, it } from 'vitest'
import { WeightUnit } from '../../api/auth'
import type { ExerciseTarget } from '../../api/sessions'
import { displayWeight } from '../../lib/weight'
import { addDraft, applyTarget, canRemoveLast, effortLabel, emptyDraft, MAX_SETS, removeLastDraft, toEntries } from './sets'

const target: ExerciseTarget = {
  order: 1,
  exerciseId: 'e1',
  sets: 3,
  repsMin: 8,
  repsMax: 12,
  weightKg: 62.5,
  reps: 8,
  reason: 'IncreaseWeight',
  lastWeightKg: 60,
  lastReps: [12, 12, 12],
}

describe('smart sets', () => {
  it('adds a set that copies the previous one and stops at the maximum', () => {
    const drafts = [{ weight: '60', reps: '8', done: true, effort: 2 }]

    expect(addDraft(drafts)).toEqual([drafts[0], { weight: '60', reps: '8', done: false, effort: null }])
    expect(addDraft([])).toEqual([emptyDraft()])
    const full = Array.from({ length: MAX_SETS }, emptyDraft)
    expect(addDraft(full)).toBe(full)
  })

  it('removes only an unfinished last set and keeps at least one', () => {
    const open = [emptyDraft(), emptyDraft()]
    const done = [emptyDraft(), { ...emptyDraft(), done: true }]

    expect(canRemoveLast(open)).toBe(true)
    expect(removeLastDraft(open)).toHaveLength(1)
    expect(canRemoveLast(done)).toBe(false)
    expect(removeLastDraft(done)).toBe(done)
    expect(canRemoveLast([emptyDraft()])).toBe(false)
  })

  it('fills unfinished sets with the target in the user unit', () => {
    const drafts = [{ weight: '50', reps: '5', done: true, effort: null }, emptyDraft()]

    expect(applyTarget(drafts, target, WeightUnit.Kilograms)).toEqual([drafts[0], { weight: '62.5', reps: '8', done: false, effort: null }])
    expect(applyTarget([emptyDraft()], target, WeightUnit.Pounds)[0].weight).toBe('138')
    expect(applyTarget([{ ...emptyDraft(), weight: '40' }], { ...target, weightKg: null }, WeightUnit.Kilograms)[0]).toEqual({
      weight: '40',
      reps: '8',
      done: false,
      effort: null,
    })
  })

  it('sends the effort only when one was chosen', () => {
    const entries = toEntries(
      [
        { weight: '60', reps: '8', done: true, effort: 0 },
        { weight: '60', reps: '8', done: true, effort: null },
      ],
      WeightUnit.Kilograms,
    )

    expect(entries).toEqual([{ reps: 8, weightKg: 60, repsInReserve: 0 }, { reps: 8, weightKg: 60 }])
  })

  it('labels the effort scale', () => {
    expect([3, 2, 1, 0, null, 7].map(effortLabel)).toEqual(['Easy', 'Good', 'Hard', 'Max', null, null])
  })

  it('rounds displayed weights to plate friendly steps', () => {
    expect(displayWeight(62.5, WeightUnit.Kilograms)).toBe(62.5)
    expect(displayWeight(61.23, WeightUnit.Kilograms)).toBe(61.25)
    expect(displayWeight(62.5, WeightUnit.Pounds)).toBe(138)
    expect(displayWeight(61.23, WeightUnit.Pounds)).toBe(135)
  })
})
