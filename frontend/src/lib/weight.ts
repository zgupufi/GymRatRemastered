import { WeightUnit } from '../api/auth'

const POUNDS_PER_KILOGRAM = 2.20462

export function unitLabel(unit: WeightUnit): string {
  return unit === WeightUnit.Pounds ? 'Lbs' : 'Kg'
}

export function fromKilograms(weightKg: number, unit: WeightUnit): number {
  return unit === WeightUnit.Pounds ? Math.round(weightKg * POUNDS_PER_KILOGRAM) : Math.round(weightKg)
}

const KILOGRAM_DISPLAY_STEP = 0.25
const POUND_DISPLAY_STEP = 0.5

export function displayWeight(weightKg: number, unit: WeightUnit): number {
  if (unit === WeightUnit.Pounds) {
    return Math.round((weightKg * POUNDS_PER_KILOGRAM) / POUND_DISPLAY_STEP) * POUND_DISPLAY_STEP
  }
  return Math.round(weightKg / KILOGRAM_DISPLAY_STEP) * KILOGRAM_DISPLAY_STEP
}

export function toKilograms(weight: number, unit: WeightUnit): number {
  if (unit !== WeightUnit.Pounds) {
    return weight
  }
  const exact = weight / POUNDS_PER_KILOGRAM
  const snapped = Math.round(exact / KILOGRAM_DISPLAY_STEP) * KILOGRAM_DISPLAY_STEP
  return displayWeight(snapped, unit) === weight ? snapped : Math.round(exact * 100) / 100
}
