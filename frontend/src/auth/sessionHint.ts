const HINT_KEY = 'gymrat.signedIn'
const LEGACY_TOKEN_KEY = 'gymrat.session'

export function hasSessionHint(): boolean {
  try {
    localStorage.removeItem(LEGACY_TOKEN_KEY)
    return localStorage.getItem(HINT_KEY) === '1'
  } catch {
    return false
  }
}

export function markSessionActive(): void {
  try {
    localStorage.setItem(HINT_KEY, '1')
  } catch {
    return
  }
}

export function clearSessionHint(): void {
  try {
    localStorage.removeItem(HINT_KEY)
  } catch {
    return
  }
}
