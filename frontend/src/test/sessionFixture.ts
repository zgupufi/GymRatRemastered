import { vi } from 'vitest'
import { markSessionActive } from '../auth/sessionHint'

export const FIXTURE_ACCESS_TOKEN = 'token'

export function givenRestorableSession(): void {
  markSessionActive()
  vi.stubGlobal(
    'fetch',
    vi.fn<typeof fetch>().mockImplementation(() =>
      Promise.resolve(Response.json({ accessToken: FIXTURE_ACCESS_TOKEN, expiresAt: '2999-01-01T00:00:00Z' })),
    ),
  )
}
