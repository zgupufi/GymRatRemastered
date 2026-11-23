import { afterEach, describe, expect, it, vi } from 'vitest'
import { WeightUnit } from './auth'
import { changePassword, deleteAccount, downloadExport, downloadSetsCsv, updateUserSettings } from './users'

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

describe('account endpoints', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts the current and new password', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await changePassword('old', 'new')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/users/me/password')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'POST',
      body: '{"currentPassword":"old","newPassword":"new"}',
    })
  })

  it('deletes the account with the password in the body', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await deleteAccount('secret')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/users/me')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'DELETE', body: '{"password":"secret"}' })
  })

  it('downloads the JSON export and the sets CSV', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response('{}'))
      .mockResolvedValueOnce(new Response('a,b'))
    vi.stubGlobal('fetch', fetchMock)

    expect(await (await downloadExport()).text()).toBe('{}')
    expect(await (await downloadSetsCsv()).text()).toBe('a,b')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/users/me/export')
    expect(fetchMock.mock.calls[1][0]).toBe('/api/users/me/export/sets')
  })
})
