import { afterEach, describe, expect, it, vi } from 'vitest'
import { WeightUnit } from './auth'
import { updateUserSettings } from './users'

describe('updateUserSettings', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('patches the settings endpoint with the new values', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await updateUserSettings({ weightUnit: WeightUnit.Pounds, defaultRestTimeSeconds: 120 })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/users/me/settings')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'PATCH',
      body: '{"weightUnit":1,"defaultRestTimeSeconds":120}',
    })
  })
})
