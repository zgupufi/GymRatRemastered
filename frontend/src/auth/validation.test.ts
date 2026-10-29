import { describe, expect, it } from 'vitest'
import {
  validateEmail,
  validateFirstName,
  validateLastName,
  validateLoginPassword,
  validateNewPassword,
} from './validation'

describe('validation', () => {
  it('asks for an email, then a valid one', () => {
    expect(validateEmail('  ')).toBe('Enter your email address.')
    expect(validateEmail('not-an-email')).toBe('Enter a valid email address.')
    expect(validateEmail(`${'a'.repeat(100)}@example.com`)).toBe('Enter a valid email address.')
    expect(validateEmail(' name@example.com ')).toBeUndefined()
  })

  it('asks for a login password', () => {
    expect(validateLoginPassword('')).toBe('Enter your password.')
    expect(validateLoginPassword('x')).toBeUndefined()
  })

  it('requires a new password of at least 8 characters', () => {
    expect(validateNewPassword('')).toBe('Choose a password.')
    expect(validateNewPassword('short')).toBe('Use at least 8 characters.')
    expect(validateNewPassword('long-enough')).toBeUndefined()
  })

  it('requires a first name of at most 50 characters', () => {
    expect(validateFirstName(' ')).toBe('Enter your first name.')
    expect(validateFirstName('a'.repeat(51))).toBe('Use at most 50 characters.')
    expect(validateFirstName('Rafael')).toBeUndefined()
  })

  it('allows an empty last name but not a long one', () => {
    expect(validateLastName('')).toBeUndefined()
    expect(validateLastName('a'.repeat(51))).toBe('Use at most 50 characters.')
  })
})
