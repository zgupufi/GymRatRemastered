import { afterEach, describe, expect, it, vi } from 'vitest'
import { applySuggestion, dismissSuggestion, getPendingSuggestions } from './coach'
import { getTargets, saveExerciseSets } from './sessions'

function stubFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('coach and targets api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists pending suggestions', async () => {
    const fetchMock = stubFetch(Response.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }))

    expect(await getPendingSuggestions()).toEqual({ items: [], page: 1, pageSize: 100, totalCount: 0 })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/coach/suggestions?pagination.pageSize=100')
  })

  it('applies and dismisses a suggestion', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await applySuggestion('c1')
    await dismissSuggestion('c2')

    expect(fetchMock.mock.calls.map((call) => [call[0], call[1]?.method])).toEqual([
      ['/api/coach/suggestions/c1/apply', 'POST'],
      ['/api/coach/suggestions/c2/dismiss', 'POST'],
    ])
  })

  it('reads the targets of a workout', async () => {
    const fetchMock = stubFetch(Response.json([]))

    expect(await getTargets('w1')).toEqual([])
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w1/targets')
  })

  it('sends the served target with the sets', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await saveExerciseSets('s1', 'e1', [{ reps: 8, weightKg: 60, repsInReserve: 2 }], { weightKg: 60, reps: 8, reason: 'AddRep' })

    expect(fetchMock.mock.calls[0][1]?.body).toBe(
      JSON.stringify({ sets: [{ reps: 8, weightKg: 60, repsInReserve: 2 }], target: { weightKg: 60, reps: 8, reason: 'AddRep' } }),
    )
  })
})
