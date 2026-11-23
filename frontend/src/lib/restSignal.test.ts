import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

class FakeAudioContext {
  static instances: FakeAudioContext[] = []
  state: AudioContextState = 'suspended'
  currentTime = 0
  destination = {}
  oscillators: { start: ReturnType<typeof vi.fn> }[] = []
  resume = vi.fn(() => {
    this.state = 'running'
    return Promise.resolve()
  })

  constructor() {
    FakeAudioContext.instances.push(this)
  }

  createOscillator() {
    const oscillator = { frequency: { value: 0 }, connect: vi.fn(), start: vi.fn(), stop: vi.fn() }
    this.oscillators.push(oscillator)
    return oscillator
  }

  createGain() {
    return { gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() }, connect: vi.fn() }
  }
}

describe('restSignal', () => {
  const vibrate = vi.fn<(pattern: VibratePattern) => boolean>().mockReturnValue(true)

  beforeEach(() => {
    vi.resetModules()
    FakeAudioContext.instances = []
    vibrate.mockClear()
    localStorage.clear()
    Object.defineProperty(navigator, 'vibrate', { value: vibrate, configurable: true })
    vi.stubGlobal('AudioContext', FakeAudioContext)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('stores the sound setting and defaults to on', async () => {
    const { readSoundEnabled, writeSoundEnabled } = await import('./restSignal')
    expect(readSoundEnabled()).toBe(true)

    writeSoundEnabled(false)
    expect(readSoundEnabled()).toBe(false)

    writeSoundEnabled(true)
    expect(readSoundEnabled()).toBe(true)
  })

  it('survives storage that throws', async () => {
    const { readSoundEnabled, writeSoundEnabled } = await import('./restSignal')
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(readSoundEnabled()).toBe(true)
    expect(() => writeSoundEnabled(false)).not.toThrow()
  })

  it('vibrates and plays two tones when sound is on', async () => {
    const { signalRestOver } = await import('./restSignal')

    signalRestOver(true)

    expect(vibrate).toHaveBeenCalledTimes(1)
    expect(FakeAudioContext.instances).toHaveLength(1)
    expect(FakeAudioContext.instances[0].oscillators).toHaveLength(2)
  })

  it('vibrates without a tone when sound is off', async () => {
    const { signalRestOver } = await import('./restSignal')

    signalRestOver(false)

    expect(vibrate).toHaveBeenCalledTimes(1)
    expect(FakeAudioContext.instances).toHaveLength(0)
  })

  it('resumes a suspended audio context when prepared and reuses it', async () => {
    const { prepareRestSignal, signalRestOver } = await import('./restSignal')

    prepareRestSignal()
    prepareRestSignal()
    signalRestOver(true)

    expect(FakeAudioContext.instances).toHaveLength(1)
    expect(FakeAudioContext.instances[0].resume).toHaveBeenCalledTimes(1)
  })

  it('does nothing when the browser has no vibration or audio support', async () => {
    Object.defineProperty(navigator, 'vibrate', { value: undefined, configurable: true })
    vi.stubGlobal('AudioContext', undefined)
    const { prepareRestSignal, signalRestOver } = await import('./restSignal')

    expect(() => prepareRestSignal()).not.toThrow()
    expect(() => signalRestOver(true)).not.toThrow()
  })
})
