const HAPTICS_KEY = 'gymrat.haptics'
const SET_COMPLETED_PATTERN = 30
const WORKOUT_FINISHED_PATTERN = [100, 80, 100, 80, 300]
const REST_OVER_PATTERN = [200, 100, 200]

export function readHapticsEnabled(): boolean {
  try {
    return localStorage.getItem(HAPTICS_KEY) !== 'off'
  } catch {
    return true
  }
}

export function writeHapticsEnabled(enabled: boolean): void {
  try {
    localStorage.setItem(HAPTICS_KEY, enabled ? 'on' : 'off')
  } catch {
    return
  }
}

function vibrate(pattern: VibratePattern): void {
  if (readHapticsEnabled() && typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
    navigator.vibrate(pattern)
  }
}

export function setCompletedHaptic(): void {
  vibrate(SET_COMPLETED_PATTERN)
}

export function workoutFinishedHaptic(): void {
  vibrate(WORKOUT_FINISHED_PATTERN)
}

export function restOverHaptic(): void {
  vibrate(REST_OVER_PATTERN)
}
