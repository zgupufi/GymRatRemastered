import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ProgressPoint } from '../../api/progress'
import { ProgressChart } from './ProgressChart'

const points: ProgressPoint[] = [
  { date: '2026-09-12', bestSet: { reps: 8, weightKg: 70 }, estimatedOneRepMax: 88.7 },
  { date: '2026-10-10', bestSet: { reps: 6, weightKg: 80 }, estimatedOneRepMax: 96 },
  { date: '2026-11-07', bestSet: { reps: 6, weightKg: 90 }, estimatedOneRepMax: 108 },
]

describe('ProgressChart', () => {
  it('describes the chart and shows the latest session in the tooltip', () => {
    render(<ProgressChart exerciseName="Bench Press" points={points} />)

    expect(screen.getByRole('img', { name: 'Estimated one rep max of Bench Press over 3 sessions' })).toBeInTheDocument()
    expect(screen.getByText('7 Nov 2026')).toBeInTheDocument()
    expect(screen.getByText('90 kg × 6 reps')).toBeInTheDocument()
    expect(screen.getByText('108')).toBeInTheDocument()
  })

  it('moves the tooltip to the focused session', () => {
    render(<ProgressChart exerciseName="Bench Press" points={points} />)

    fireEvent.focus(screen.getByRole('button', { name: '12 Sep 2026, 89 kg estimated one rep max' }))

    expect(screen.getByText('12 Sep 2026')).toBeInTheDocument()
    expect(screen.getByText('70 kg × 8 reps')).toBeInTheDocument()
  })

  it('selects a session with Enter or Space and ignores other keys', () => {
    render(<ProgressChart exerciseName="Bench Press" points={points} />)
    const buttons = screen.getAllByRole('button')

    fireEvent.keyDown(buttons[0], { key: 'Enter' })
    expect(screen.getByText('12 Sep 2026')).toBeInTheDocument()

    fireEvent.keyDown(buttons[1], { key: ' ' })
    expect(screen.getByText('10 Oct 2026')).toBeInTheDocument()

    fireEvent.keyDown(buttons[2], { key: 'a' })
    expect(screen.getByText('10 Oct 2026')).toBeInTheDocument()
  })

  it('follows the pointer across the plot', () => {
    const { container } = render(<ProgressChart exerciseName="Bench Press" points={points} />)
    const surface = container.querySelector('.progress-chart__surface') as SVGRectElement
    surface.getBoundingClientRect = () => ({ left: 0, width: 100 }) as DOMRect

    fireEvent.pointerMove(surface, { clientX: 50 })
    expect(screen.getByText('10 Oct 2026')).toBeInTheDocument()

    fireEvent.pointerDown(surface, { clientX: 0 })
    expect(screen.getByText('12 Sep 2026')).toBeInTheDocument()

    fireEvent.pointerMove(surface, { clientX: 500 })
    expect(screen.getByText('7 Nov 2026')).toBeInTheDocument()
  })

  it('falls back to the last session when the plot has no width', () => {
    const { container } = render(<ProgressChart exerciseName="Bench Press" points={points} />)
    const surface = container.querySelector('.progress-chart__surface') as SVGRectElement
    surface.getBoundingClientRect = () => ({ left: 0, width: 0 }) as DOMRect

    fireEvent.pointerMove(surface, { clientX: 10 })

    expect(screen.getByText('7 Nov 2026')).toBeInTheDocument()
  })

  it('keeps the tooltip inside the chart on the left half', () => {
    const { container } = render(<ProgressChart exerciseName="Bench Press" points={points} />)

    fireEvent.focus(screen.getAllByRole('button')[0])

    expect(container.querySelector<HTMLElement>('.progress-chart__tooltip')?.style.left).toMatch(/\+ 16px\)$/)
  })
})
