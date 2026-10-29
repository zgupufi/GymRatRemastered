import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { installHaptics, tapHaptic } from './haptics'

function press(element: Element) {
  element.dispatchEvent(new Event('pointerdown', { bubbles: true }))
}

describe('haptics', () => {
  const vibrate = vi.fn<(pattern: VibratePattern) => boolean>().mockReturnValue(true)

  beforeEach(() => {
    Object.defineProperty(navigator, 'vibrate', { value: vibrate, configurable: true })
  })

  afterEach(() => {
    vibrate.mockClear()
    document.body.innerHTML = ''
  })

  it('vibrates for a tap', () => {
    tapHaptic()

    expect(vibrate).toHaveBeenCalledWith(15)
  })

  it('vibrates when a button is pressed', () => {
    const uninstall = installHaptics()
    document.body.innerHTML = '<button><span id="inner">Go</span></button>'

    press(document.getElementById('inner')!)

    expect(vibrate).toHaveBeenCalledTimes(1)
    uninstall()
  })

  it('ignores disabled controls and plain content', () => {
    const uninstall = installHaptics()
    document.body.innerHTML = '<button disabled id="b">Go</button><p id="p">Text</p>'

    press(document.getElementById('b')!)
    press(document.getElementById('p')!)

    expect(vibrate).not.toHaveBeenCalled()
    uninstall()
  })

  it('does nothing when the Vibration API is missing', () => {
    Object.defineProperty(navigator, 'vibrate', { value: undefined, configurable: true })

    expect(() => tapHaptic()).not.toThrow()
  })

  it('ignores presses whose target is not an element', () => {
    const uninstall = installHaptics()

    document.dispatchEvent(new Event('pointerdown'))

    expect(vibrate).not.toHaveBeenCalled()
    uninstall()
  })
})
