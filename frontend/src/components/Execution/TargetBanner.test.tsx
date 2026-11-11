import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { WeightUnit } from '../../api/auth'
import type { ExerciseTarget } from '../../api/sessions'
import { TargetBanner } from './TargetBanner'

const base: ExerciseTarget = {
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

describe('TargetBanner', () => {
  it('does not crash on an unexpected reason or missing last reps', () => {
    const odd = { ...base, reason: 0, lastReps: undefined } as unknown as ExerciseTarget
    render(<TargetBanner target={odd} unit={WeightUnit.Kilograms} onUse={vi.fn()} />)

    expect(screen.getByText('Add a rep')).toBeInTheDocument()
    expect(screen.getByText('Last time 60 kg ×')).toBeInTheDocument()
  })

  it('shows a new weight with last time and uses the target', async () => {
    const onUse = vi.fn()
    render(<TargetBanner target={base} unit={WeightUnit.Kilograms} onUse={onUse} />)

    expect(screen.getByText('New weight')).toBeInTheDocument()
    expect(screen.getByText('62.5 kg × 8')).toBeInTheDocument()
    expect(screen.getByText('Last time 60 kg × 12, 12, 12')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Use target' }))
    expect(onUse).toHaveBeenCalledTimes(1)
  })

  it('asks for a weight the first time without a use button', () => {
    render(
      <TargetBanner target={{ ...base, weightKg: null, reason: 'NoHistory', lastWeightKg: null, lastReps: [] }} unit={WeightUnit.Kilograms} onUse={vi.fn()} />,
    )

    expect(screen.getByText('First time')).toBeInTheDocument()
    expect(screen.getByText('Pick a weight, aim for 8–12')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Use target' })).not.toBeInTheDocument()
  })

  it('explains a hold, a stall and bodyweight targets in pounds', () => {
    const { rerender } = render(<TargetBanner target={{ ...base, reason: 'Hold', weightKg: 100 }} unit={WeightUnit.Pounds} onUse={vi.fn()} />)

    expect(screen.getByText('220.5 lbs × 8')).toBeInTheDocument()
    expect(screen.getByText('Same weight and reps until it feels easier.')).toBeInTheDocument()

    rerender(<TargetBanner target={{ ...base, reason: 'Stalled', weightKg: 0, lastWeightKg: 0, reps: 10 }} unit={WeightUnit.Kilograms} onUse={vi.fn()} />)

    expect(screen.getByText('Stalled')).toBeInTheDocument()
    expect(screen.getByText('Bodyweight × 10')).toBeInTheDocument()
    expect(screen.getByText('Stalled two sessions in a row. Ask your coach for a plan.')).toBeInTheDocument()

    rerender(<TargetBanner target={{ ...base, reason: 'AddRep', reps: 9 }} unit={WeightUnit.Kilograms} onUse={vi.fn()} />)
    expect(screen.getByText('Add a rep')).toBeInTheDocument()
  })
})
