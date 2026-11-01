import { WeightUnit } from '../api/auth'

const POUNDS_PER_KILOGRAM = 2.20462

export function unitLabel(unit: WeightUnit): string {
  return unit === WeightUnit.Pounds ? 'Lbs' : 'Kg'
}

export function fromKilograms(weightKg: number, unit: WeightUnit): number {
  return unit === WeightUnit.Pounds ? Math.round(weightKg * POUNDS_PER_KILOGRAM) : Math.round(weightKg)
}

export function toKilograms(weight: number, unit: WeightUnit): number {
  return unit === WeightUnit.Pounds ? Math.round((weight / POUNDS_PER_KILOGRAM) * 100) / 100 : weight
}
