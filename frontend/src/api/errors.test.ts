import { describe, expect, it } from 'vitest'
import { ApiError } from './client'
import { friendlyErrorMessage } from './errors'

describe('friendlyErrorMessage', () => {
  it.each([
    [0, 'Could not reach the server'],
    [503, 'waking up'],
    [504, 'waking up'],
    [401, 'log in again'],
    [403, 'log in again'],
    [404, 'could not find'],
    [500, 'ran into an issue'],
  ])('maps status %i to a friendly message', (status, fragment) => {
    expect(friendlyErrorMessage(new ApiError(status, 'raw'))).toContain(fragment)
  })

  it('shows the backend message for a domain error', () => {
    const error = new ApiError(400, 'Name is required.', 'Exercises.NameRequired')

    expect(friendlyErrorMessage(error)).toBe('Name is required.')
  })

  it('falls back for unknown errors', () => {
    expect(friendlyErrorMessage(new Error('boom'))).toBe('Something went wrong. Please try again.')
    expect(friendlyErrorMessage(new ApiError(400, 'raw'))).toBe('Something went wrong. Please try again.')
  })
})
