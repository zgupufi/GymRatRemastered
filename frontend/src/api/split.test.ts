import { afterEach, describe, expect, it, vi } from 'vitest'
import { assignSplitDay, getSplit } from './split'

function stubFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('split api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the split', async () => {
    const days = [{ day: 1, workout: null }]
    const fetchMock = stubFetch(Response.json(days))

    expect(await getSplit()).toEqual(days)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/split')
  })

  it('assigns a workout to a day', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await assignSplitDay(3, 'w1')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/split/3')
    expect(fetchMock.mock.calls[0][1]?.method).toBe('PUT')
    expect(fetchMock.mock.calls[0][1]?.body).toBe(JSON.stringify({ workoutId: 'w1' }))
  })

  it('clears a day', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await assignSplitDay(3, null)

    expect(fetchMock.mock.calls[0][1]?.body).toBe(JSON.stringify({ workoutId: null }))
  })
})
