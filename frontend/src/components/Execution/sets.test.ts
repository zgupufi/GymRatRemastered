import { describe, expect, it } from 'vitest'
import { WeightUnit } from '../../api/auth'
import { emptyDrafts, isValidDraft, previousFor, toEntries } from './sets'

describe('sets', () => {
  it('creates the target number of empty drafts', () => {
    expect(emptyDrafts(4)).toHaveLength(4)
    expect(emptyDrafts(4)[0]).toEqual({ weight: '', reps: '', done: false })
  })

  it('defaults to three sets', () => {
    expect(emptyDrafts(0)).toHaveLength(3)
  })

  it.each([
    ['', '5', false],
    ['50', '', false],
    ['50', '0', false],
    ['50', '1001', false],
    ['50', '2.5', false],
    ['-5', '5', false],
    ['2001', '5', false],
    ['0', '5', true],
    ['50', '8', true],
  ])('validates weight %s and reps %s', (weight, reps, expected) => {
    expect(isValidDraft({ weight, reps, done: true }, WeightUnit.Kilograms)).toBe(expected)
  })

  it('only sends completed valid sets converted to kilograms', () => {
    const entries = toEntries(
      [
        { weight: '220', reps: '5', done: true },
        { weight: '100', reps: '5', done: false },
        { weight: '', reps: '5', done: true },
      ],
      WeightUnit.Pounds,
    )

    expect(entries).toEqual([{ reps: 5, weightKg: 99.79 }])
  })

  it('finds the previous set by number', () => {
    const previous = [{ setNumber: 2, reps: 8, weightKg: 50 }]

    expect(previousFor(previous, 2)).toEqual(previous[0])
    expect(previousFor(previous, 1)).toBeUndefined()
    expect(previousFor(undefined, 1)).toBeUndefined()
  })
})
