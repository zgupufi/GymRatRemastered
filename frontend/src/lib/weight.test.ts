import { describe, expect, it } from 'vitest'
import { WeightUnit } from '../api/auth'
import { formatClock } from './clock'
import { displayWeight, fromKilograms, toKilograms, unitLabel } from './weight'

describe('weight', () => {
  it('labels units', () => {
    expect(unitLabel(WeightUnit.Kilograms)).toBe('Kg')
    expect(unitLabel(WeightUnit.Pounds)).toBe('Lbs')
  })

  it('converts from kilograms for display', () => {
    expect(fromKilograms(60.4, WeightUnit.Kilograms)).toBe(60)
    expect(fromKilograms(100, WeightUnit.Pounds)).toBe(220)
  })

  it('converts to kilograms for saving', () => {
    expect(toKilograms(60, WeightUnit.Kilograms)).toBe(60)
    expect(toKilograms(220, WeightUnit.Pounds)).toBe(99.75)
    expect(toKilograms(137.3, WeightUnit.Pounds)).toBe(62.28)
  })

  it('keeps a kilogram weight stable through a pounds round trip', () => {
    for (const kg of [20, 22.5, 42.5, 62.5, 100, 102.5]) {
      const pounds = displayWeight(kg, WeightUnit.Pounds)
      expect(toKilograms(pounds, WeightUnit.Pounds)).toBe(kg)
    }
  })
})

describe('formatClock', () => {
  it('pads minutes and seconds', () => {
    expect(formatClock(0)).toBe('00:00')
    expect(formatClock(75)).toBe('01:15')
    expect(formatClock(3725)).toBe('62:05')
  })

  it('never goes negative', () => {
    expect(formatClock(-4)).toBe('00:00')
  })
})
