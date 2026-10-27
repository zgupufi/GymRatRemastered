const HAPTIC_SELECTOR = 'button, a, [role="button"], [role="switch"], [role="tab"], input[type="number"]'
const TAP_DURATION_MS = 15

export function tapHaptic(): void {
  if (typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
    navigator.vibrate(TAP_DURATION_MS)
  }
}

export function installHaptics(target: Document = document): () => void {
  const onPointerDown = (event: PointerEvent) => {
    if (!(event.target instanceof Element)) {
      return
    }
    const control = event.target.closest(HAPTIC_SELECTOR)
    if (control && !control.matches(':disabled, [aria-disabled="true"]')) {
      tapHaptic()
    }
  }
  target.addEventListener('pointerdown', onPointerDown, true)
  return () => target.removeEventListener('pointerdown', onPointerDown, true)
}
