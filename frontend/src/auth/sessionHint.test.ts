import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearSessionHint, hasSessionHint, markSessionActive } from './sessionHint'

describe('sessionHint', () => {
  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('has no hint until a session is marked', () => {
    expect(hasSessionHint()).toBe(false)

    markSessionActive()

    expect(hasSessionHint()).toBe(true)
  })

  it('stores a flag and never a token', () => {
    markSessionActive()

    expect(localStorage.getItem('gymrat.signedIn')).toBe('1')
    expect(localStorage.length).toBe(1)
  })

  it('forgets the hint', () => {
    markSessionActive()

    clearSessionHint()

    expect(hasSessionHint()).toBe(false)
  })

  it('removes a token that an older version stored', () => {
    localStorage.setItem('gymrat.session', JSON.stringify({ accessToken: 'old', expiresAt: '2999-01-01T00:00:00Z' }))

    expect(hasSessionHint()).toBe(false)
    expect(localStorage.getItem('gymrat.session')).toBeNull()
  })

  it('does not throw when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(hasSessionHint()).toBe(false)
    expect(() => markSessionActive()).not.toThrow()
    expect(() => clearSessionHint()).not.toThrow()
  })
})
