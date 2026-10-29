import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  createWorkout,
  deleteWorkout,
  getWorkout,
  getWorkoutSummaries,
  renameWorkout,
  replaceWorkoutExercises,
} from './workouts'

function stubFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('workouts api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists summaries with dotted query parameters', async () => {
    const page = { items: [], page: 1, pageSize: 10, totalCount: 0 }
    const fetchMock = stubFetch(Response.json(page))

    const result = await getWorkoutSummaries({ term: 'push', sortBy: 'CreatedAt', direction: 'Descending', page: 1, pageSize: 10 })

    expect(result).toEqual(page)
    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/workouts?search.term=push&sort.by=CreatedAt&sort.direction=Descending&pagination.page=1&pagination.pageSize=10',
    )
  })

  it('reads one workout', async () => {
    const fetchMock = stubFetch(Response.json({ id: 'w1', name: 'Push', exercises: [] }))

    await getWorkout('w1')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w1')
  })

  it('creates a workout and returns its id', async () => {
    const fetchMock = stubFetch(Response.json({ id: 'w2' }, { status: 201 }))

    const result = await createWorkout('Legs')

    expect(result).toEqual({ id: 'w2' })
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'POST', body: '{"name":"Legs"}' })
  })

  it('renames with PATCH', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await renameWorkout('w1', 'Pull')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w1')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'PATCH', body: '{"name":"Pull"}' })
  })

  it('replaces the exercises with PUT', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await replaceWorkoutExercises('w1', [{ exerciseId: 'e1', targetSets: 4 }])

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w1/exercises')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'PUT', body: '{"exercises":[{"exerciseId":"e1","targetSets":4}]}' })
  })

  it('deletes a workout', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await deleteWorkout('w1')

    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'DELETE' })
  })
})
