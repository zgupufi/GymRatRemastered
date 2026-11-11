import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useRestTimer } from './useRestTimer'
import { useStopwatch } from './useStopwatch'

describe('timers', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('continues from the time a session was started', () => {
    vi.setSystemTime(new Date('2026-01-01T10:10:00Z'))
    const { result } = renderHook(() => useStopwatch(true, Date.parse('2026-01-01T10:00:00Z')))
    expect(result.current).toBe(600)

    act(() => {
      vi.advanceTimersByTime(3000)
    })
    expect(result.current).toBe(603)
  })

  it('never shows a negative time when the clock is behind the session start', () => {
    vi.setSystemTime(new Date('2026-01-01T10:00:00Z'))
    const { result } = renderHook(() => useStopwatch(true, Date.parse('2026-01-01T10:00:05Z')))
    expect(result.current).toBe(0)

    act(() => {
      vi.advanceTimersByTime(2000)
    })
    expect(result.current).toBe(0)
  })

  it('counts elapsed seconds only while running', () => {
    const { result, rerender } = renderHook(({ running }) => useStopwatch(running), { initialProps: { running: false } })
    act(() => {
      vi.advanceTimersByTime(3000)
    })
    expect(result.current).toBe(0)

    rerender({ running: true })
    act(() => {
      vi.advanceTimersByTime(5000)
    })
    expect(result.current).toBe(5)

    rerender({ running: false })
    rerender({ running: true })
    act(() => {
      vi.advanceTimersByTime(2000)
    })
    expect(result.current).toBe(7)
  })

  it('counts the rest down and clears at zero', () => {
    const { result } = renderHook(() => useRestTimer())
    expect(result.current.remaining).toBeNull()

    act(() => result.current.start(2))
    expect(result.current.remaining).toBe(2)
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current.remaining).toBe(1)
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current.remaining).toBeNull()
  })

  it('ignores a zero length rest', () => {
    const { result } = renderHook(() => useRestTimer())

    act(() => result.current.start(0))

    expect(result.current.remaining).toBeNull()
  })

  it('extends, reduces and skips', () => {
    const { result } = renderHook(() => useRestTimer())
    act(() => result.current.extend())
    act(() => result.current.reduce())
    expect(result.current.remaining).toBeNull()

    act(() => result.current.start(60))
    act(() => result.current.extend())
    expect(result.current.remaining).toBe(90)
    act(() => result.current.reduce())
    expect(result.current.remaining).toBe(80)
    act(() => result.current.skip())
    expect(result.current.remaining).toBeNull()
  })

  it('stops the rest when reduced to zero', () => {
    const { result } = renderHook(() => useRestTimer())
    act(() => result.current.start(8))

    act(() => result.current.reduce())

    expect(result.current.remaining).toBeNull()
  })
})
