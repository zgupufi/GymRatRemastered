import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  readHapticsEnabled,
  restOverHaptic,
  setCompletedHaptic,
  workoutFinishedHaptic,
  writeHapticsEnabled,
} from './haptics'

describe('haptics', () => {
  const vibrate = vi.fn<(pattern: VibratePattern) => boolean>().mockReturnValue(true)

  beforeEach(() => {
    Object.defineProperty(navigator, 'vibrate', { value: vibrate, configurable: true })
  })

  afterEach(() => {
    vibrate.mockClear()
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it.each([
    ['a completed set', setCompletedHaptic, 30],
    ['a finished workout', workoutFinishedHaptic, [100, 80, 100, 80, 300]],
    ['the end of rest', restOverHaptic, [200, 100, 200]],
  ])('vibrates for %s', (_, haptic, pattern) => {
    haptic()

    expect(vibrate).toHaveBeenCalledExactlyOnceWith(pattern)
  })

  it('stays still for every event when turned off', () => {
    writeHapticsEnabled(false)

    setCompletedHaptic()
    workoutFinishedHaptic()
    restOverHaptic()

    expect(vibrate).not.toHaveBeenCalled()
  })

  it('stores the setting and defaults to on', () => {
    expect(readHapticsEnabled()).toBe(true)

    writeHapticsEnabled(false)
    expect(readHapticsEnabled()).toBe(false)

    writeHapticsEnabled(true)
    expect(readHapticsEnabled()).toBe(true)
  })

  it('survives storage that throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(readHapticsEnabled()).toBe(true)
    expect(() => writeHapticsEnabled(false)).not.toThrow()
  })

  it('does nothing when the Vibration API is missing', () => {
    Object.defineProperty(navigator, 'vibrate', { value: undefined, configurable: true })

    expect(() => setCompletedHaptic()).not.toThrow()
  })
})
