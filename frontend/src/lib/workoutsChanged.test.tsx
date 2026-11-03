import { renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { announceWorkoutsChanged, useWorkoutsChanged } from './workoutsChanged'

describe('workoutsChanged', () => {
  it('calls the latest callback when workouts change and stops after unmount', () => {
    const first = vi.fn()
    const second = vi.fn()
    const { rerender, unmount } = renderHook(({ callback }) => useWorkoutsChanged(callback), {
      initialProps: { callback: first },
    })

    rerender({ callback: second })
    announceWorkoutsChanged()
    expect(first).not.toHaveBeenCalled()
    expect(second).toHaveBeenCalledTimes(1)

    unmount()
    announceWorkoutsChanged()
    expect(second).toHaveBeenCalledTimes(1)
  })
})
