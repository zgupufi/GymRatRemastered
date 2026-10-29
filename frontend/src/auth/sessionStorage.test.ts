import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearSession, loadSession, saveSession } from './sessionStorage'

const FUTURE = '2999-01-01T00:00:00Z'

describe('sessionStorage', () => {
  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('returns a saved session that has not expired', () => {
    saveSession({ accessToken: 'abc', expiresAt: FUTURE })

    expect(loadSession()).toEqual({ accessToken: 'abc', expiresAt: FUTURE })
  })

  it('drops an expired session', () => {
    saveSession({ accessToken: 'abc', expiresAt: '2020-01-01T00:00:00Z' })

    expect(loadSession()).toBeNull()
    expect(localStorage.getItem('gymrat.session')).toBeNull()
  })

  it('returns null when nothing is stored', () => {
    expect(loadSession()).toBeNull()
  })

  it('drops a stored value that is not a session', () => {
    localStorage.setItem('gymrat.session', JSON.stringify({ accessToken: 1 }))

    expect(loadSession()).toBeNull()
    expect(localStorage.getItem('gymrat.session')).toBeNull()
  })

  it('returns null when the stored value is not valid JSON', () => {
    localStorage.setItem('gymrat.session', '{nope')

    expect(loadSession()).toBeNull()
  })

  it('clears the session', () => {
    saveSession({ accessToken: 'abc', expiresAt: FUTURE })

    clearSession()

    expect(loadSession()).toBeNull()
  })

  it('does not throw when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(() => saveSession({ accessToken: 'abc', expiresAt: FUTURE })).not.toThrow()
    expect(() => clearSession()).not.toThrow()
  })
})
