import { ApiError } from './client'

const FALLBACK = 'Something went wrong. Please try again.'

export function friendlyErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return FALLBACK
  }
  if (error.status === 0) {
    return 'Could not reach the server. Check your connection and try again.'
  }
  if (error.status === 503 || error.status === 504) {
    return 'The server is waking up after being idle. Give it a few seconds and try again.'
  }
  if (error.status === 401 || error.status === 403) {
    return 'Your session is not valid anymore. Please log in again.'
  }
  if (error.status === 404) {
    return 'We could not find what you were looking for.'
  }
  if (error.status >= 500) {
    return 'The server ran into an issue. Please try again in a moment.'
  }
  if (error.code) {
    return error.message
  }
  return FALLBACK
}
