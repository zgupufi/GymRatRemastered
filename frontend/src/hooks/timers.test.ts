import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useRestTimer } from './useRestTimer'
import { useStopwatch } from './useStopwatch'
import * as restSignal from '../lib/restSignal'

describe('timers', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    localStorage.clear()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
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
    expect(result.current.finished).toBe(true)
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
  it('catches up after the page was hidden and signals once', () => {
    const signal = vi.spyOn(restSignal, 'signalRestOver').mockImplementation(() => undefined)
    const { result } = renderHook(() => useRestTimer())
    act(() => result.current.start(90))

    vi.setSystemTime(Date.now() + 60_000)
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    expect(result.current.remaining).toBe(30)
    expect(signal).not.toHaveBeenCalled()

    vi.setSystemTime(Date.now() + 40_000)
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    expect(result.current.remaining).toBeNull()
    expect(result.current.finished).toBe(true)
    act(() => {
      vi.advanceTimersByTime(2000)
      document.dispatchEvent(new Event('visibilitychange'))
    })
    expect(signal).toHaveBeenCalledTimes(1)
    expect(signal).toHaveBeenCalledWith(true)
  })

  it('shows the rest as over for a few seconds then clears it', () => {
    vi.spyOn(restSignal, 'signalRestOver').mockImplementation(() => undefined)
    const { result } = renderHook(() => useRestTimer())
    act(() => result.current.start(1))
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current.finished).toBe(true)

    act(() => {
      vi.advanceTimersByTime(4000)
    })
    expect(result.current.finished).toBe(false)
  })

  it('does not signal when rest is skipped or reduced to zero', () => {
    const signal = vi.spyOn(restSignal, 'signalRestOver').mockImplementation(() => undefined)
    const { result } = renderHook(() => useRestTimer())
    act(() => result.current.start(5))
    act(() => result.current.skip())
    act(() => result.current.start(5))
    act(() => result.current.reduce())
    act(() => {
      vi.advanceTimersByTime(10_000)
    })

    expect(signal).not.toHaveBeenCalled()
    expect(result.current.finished).toBe(false)
  })

  it('starting a new rest clears the finished state', () => {
    vi.spyOn(restSignal, 'signalRestOver').mockImplementation(() => undefined)
    const { result } = renderHook(() => useRestTimer())
    act(() => result.current.start(1))
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current.finished).toBe(true)

    act(() => result.current.start(30))
    expect(result.current.finished).toBe(false)
    expect(result.current.remaining).toBe(30)
  })

  it('passes the sound setting to the signal and persists the toggle', () => {
    const signal = vi.spyOn(restSignal, 'signalRestOver').mockImplementation(() => undefined)
    const { result } = renderHook(() => useRestTimer())
    expect(result.current.soundEnabled).toBe(true)

    act(() => result.current.toggleSound())
    expect(result.current.soundEnabled).toBe(false)
    expect(localStorage.getItem('gymrat.restSound')).toBe('off')

    act(() => result.current.start(1))
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(signal).toHaveBeenCalledWith(false)

    const { result: next } = renderHook(() => useRestTimer())
    expect(next.current.soundEnabled).toBe(false)
  })
})
